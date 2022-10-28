namespace Odyssey;

using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using O9d.Guard;

public sealed class EventStore : IEventStore
{
    private static readonly TransactionalBatchItemRequestOptions DefaultBatchOptions = new()
    {
        EnableContentResponseOnWrite = false
    };

    private readonly ILogger<EventStore> _logger;
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseName;
    private readonly JsonSerializer _serializer;

    private Database _database = null!;
    private Container _container = null!;

    public EventStore(CosmosClient cosmosClient, string databaseName, ILoggerFactory loggerFactory)
    {
        _cosmosClient = cosmosClient.NotNull();
        _databaseName = databaseName.NotNullOrWhiteSpace();
        _logger = loggerFactory.NotNull().CreateLogger<EventStore>();

        _serializer = JsonSerializer.Create(SerializerSettings.Default);
    }

    // Could be abstracted
    public async Task Initialize(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var throughputProperties = ThroughputProperties.CreateAutoscaleThroughput(1000); // TODO configurable

        var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName, throughputProperties, cancellationToken: cancellationToken);

        _database = databaseResponse.Database;

        cancellationToken.ThrowIfCancellationRequested();
        var containerResponse = await CreateContainerIfNotExists(_database, cancellationToken);

        _container = containerResponse.Container;

        //cancellationToken.ThrowIfCancellationRequested();

        // await Task.WhenAll(
        //     SetDatabaseOfferThroughput(),
        //     SetCollectionOfferThroughput()
        // );
    }

    private static Task<ContainerResponse> CreateContainerIfNotExists(Database database, CancellationToken cancellationToken)
    {
        var containerProperties = new ContainerProperties()
        {
            Id = "events", // TODO make configurable
            IndexingPolicy = new IndexingPolicy
            {
                IncludedPaths =
                    {
                        new IncludedPath {Path = "/*"},
                    },
                ExcludedPaths =
                    {
                        new ExcludedPath {Path = "/data/*"},
                        new ExcludedPath {Path = "/metadata/*"}
                    }
            },
            //DefaultTimeToLive = collectionOptions.DefaultTimeToLive,
            PartitionKeyPath = "/stream_id"
        };

        // TODO
        // return database.CreateContainerIfNotExistsAsync(collectionProperties,
        //     collectionOptions.CollectionRequestUnits);

        return database.CreateContainerIfNotExistsAsync(containerProperties, cancellationToken: cancellationToken);
    }

    public async Task AppendToStream(string streamId, IReadOnlyList<EventData> events, StreamState expectedState, CancellationToken cancellationToken = default)
    {
        streamId.NotNullOrWhiteSpace();
        events.NotNull();

        if (events.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Append to stream {ExpectedState}@{StreamId}.", expectedState, streamId);

        try
        {
            Task<TransactionalBatchResponse> appendTask = expectedState switch
            {
                { } when expectedState == StreamState.NoStream => AppendToNewStream(streamId, events, cancellationToken),
                { } when expectedState == StreamState.StreamExists => AppendToExistingStreamAnyVersion(streamId, events, cancellationToken),
                { } when expectedState == StreamState.Any => AppendToStreamAnyState(streamId, events, cancellationToken),
                _ => AppendToStreamAtVersion(streamId, events, expectedState, true, cancellationToken)
            };

            _ = await appendTask;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict || ex.Headers["x-ms-substatus"] == "409" || ex.SubStatusCode == 409)
        {
            throw new ConcurrencyException($"Concurrency conflict when appending to stream {streamId}.", ex);
        }
    }

    /// <summary>
    /// Appends the provided events to a new stream
    /// We're able to validate that the stream does not exist by writing the first event 0@{StreamId}
    /// If the stream exists, this event would exist and therefore the CreateItem operation would fail
    /// </summary>
    private async Task<TransactionalBatchResponse> AppendToNewStream(string streamId, IReadOnlyList<EventData> events, CancellationToken cancellationToken)
    {
        TransactionalBatch batch = _container.CreateTransactionalBatch(new PartitionKey(streamId));

        for (int version = 0; version < events.Count; version++)
        {
            batch.CreateItem(CosmosEvent.FromEventData(streamId, version, events[version], _serializer), DefaultBatchOptions);
        }

        using var batchResponse = await batch.ExecuteAsync(cancellationToken);

        return batchResponse.IsSuccessStatusCode
            ? batchResponse
            : throw batchResponse.StatusCode switch
            {
                HttpStatusCode.Conflict => new CosmosException(
                    $"Stream '{streamId}' already exists",
                    HttpStatusCode.Conflict, 0, batchResponse.ActivityId, batchResponse.RequestCharge),
                _ => new CosmosException(batchResponse.ErrorMessage, batchResponse.StatusCode, 0,
                    batchResponse.ActivityId, batchResponse.RequestCharge)
            };
    }


    /// <summary>
    /// To append to an *existing* stream at any state we need to first obtain the current version (must be >= 0)
    /// Then we can append using current version as expected version
    /// </summary>
    private async Task<TransactionalBatchResponse> AppendToExistingStreamAnyVersion(string streamId, IReadOnlyList<EventData> events, CancellationToken cancellationToken)
    {
        long currentState = await GetCurrentState(streamId, cancellationToken);

        if (currentState == StreamState.NoStream)
        {
            throw new ConcurrencyException($"Stream '{streamId}' does not exist"); // Should use a specific exception type
        }

        return await AppendToStreamAtVersion(streamId, events, currentState, false, cancellationToken);
    }

    /// <summary>
    /// To append to a stream in any state we need to obtain the current version (last event) of the stream
    /// </summary>
    private async Task<TransactionalBatchResponse> AppendToStreamAnyState(string streamId, IReadOnlyList<EventData> events, CancellationToken cancellationToken)
    {
        long currentVersion = await GetCurrentState(streamId, cancellationToken);
        return await AppendToStreamAtVersion(streamId, events, currentVersion, false, cancellationToken);
    }

    /// <summary>
    /// Gets the current state of the stream
    /// </summary>
    private async Task<long> GetCurrentState(string streamId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT value COUNT(e.id) 
            FROM e
            WHERE e.stream_id = @stream_id
        ";

        var queryDefinition = new QueryDefinition(sql)
            .WithParameter("@stream_id", streamId);

        var options = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(streamId),
        };

        using var eventsQuery = _container.GetItemQueryIterator<long>(queryDefinition, requestOptions: options);

        if (!eventsQuery.HasMoreResults)
        {
            return StreamState.NoStream;
        }

        long eventCount = (await eventsQuery.ReadNextAsync(cancellationToken)).SingleOrDefault();
        return StreamState.AtVersion(eventCount - 1); // 0 based index
    }

    /// <summary>
    /// Appends to a stream at an expected version
    /// We validate this by attempting to read {ExpectedVersion}@{StreamId} within the same transaction
    /// If this fails, the stream is in an unexpected state. There are two reasons why this may happen:
    ///     * The expected version does not exist
    ///     * The stream has been updated and one of the events to append would override existing events
    /// </summary>
    private async Task<TransactionalBatchResponse> AppendToStreamAtVersion(string streamId, IReadOnlyList<EventData> events, long version, bool validateVersion, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Append to stream at {Version}@{StreamId}.", version, streamId);

        TransactionalBatch batch = _container.CreateTransactionalBatch(new PartitionKey(streamId));

        var transactionalBatchItemRequestOptions = new TransactionalBatchItemRequestOptions
        {
            EnableContentResponseOnWrite = false // Don't return the event data in the response
        };

        // If we have already validated that the version exists (e.g. Appending in any state)
        // we can skip reading the item within the batch
        if (validateVersion)
        {
            // Attempt to read the event at the expected revision
            batch.ReadItem(CosmosEvent.GenerateId(version, streamId), new TransactionalBatchItemRequestOptions { EnableContentResponseOnWrite = false });
        }

        long newVersion = version;
        for (int index = 0; index < events.Count; index++)
        {
            batch.CreateItem(CosmosEvent.FromEventData(streamId, ++newVersion, events[index], _serializer), transactionalBatchItemRequestOptions);
        }

        using var batchResponse = await batch.ExecuteAsync(cancellationToken);

        return batchResponse.IsSuccessStatusCode
            ? batchResponse
            : throw batchResponse.StatusCode switch
            {
                HttpStatusCode.Conflict => new CosmosException(
                    $"Stream '{streamId}' is not at the expected version '{version}'",
                    HttpStatusCode.Conflict, 0, batchResponse.ActivityId, batchResponse.RequestCharge),
                _ => new CosmosException(batchResponse.ErrorMessage, batchResponse.StatusCode, 0,
                    batchResponse.ActivityId, batchResponse.RequestCharge)
            };
    }

    public async Task<IReadOnlyCollection<EventData>> ReadStream(string streamId, Direction direction, StreamPosition position, CancellationToken cancellationToken = default)
    {
        streamId.NotNullOrWhiteSpace();

        // int endPosition = numberOfEventsToRead == int.MaxValue
        //     ? int.MaxValue
        //     : startPosition + numberOfEventsToRead - 1;

        var queryDefinition = new QueryDefinition(@"
            SELECT VALUE e
            FROM e
            WHERE e.stream_id = @stream_id
            ORDER BY e.event_number ASC" // Do we need to sort or is the default sort enough?
        )
        // var queryDefinition = new QueryDefinition(@"
        //     SELECT VALUE e
        //     FROM e
        //     WHERE e.streamId = @stream_id
        //         AND (e.eventNumber BETWEEN @LowerBound AND @UpperBound)
        //     ORDER BY e.eventNumber ASC"
        // )
        .WithParameter("@stream_id", streamId);
        // .WithParameter("@LowerBound", startPosition)
        // .WithParameter("@UpperBound", endPosition);

        var options = new QueryRequestOptions
        {
            //MaxItemCount = numberOfEventsToRead,
            PartitionKey = new PartitionKey(streamId)
        };

        using var eventsQuery = _container.GetItemQueryIterator<CosmosEvent>(queryDefinition, requestOptions: options);
        var events = new List<EventData>(); // could be pre-initialised to expected size

        while (eventsQuery.HasMoreResults)
        {
            var response = await eventsQuery.ReadNextAsync(cancellationToken);
            //_loggingOptions.OnSuccess(ResponseInformation.FromReadResponse(nameof(ReadStreamForwards), response));

            foreach (var @event in response)
            {
                events.Add(@event.ToEventData(_serializer));
            }
        }

        return events.AsReadOnly();
    }
}