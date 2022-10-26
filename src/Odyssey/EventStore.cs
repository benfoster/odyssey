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

        cancellationToken.ThrowIfCancellationRequested();
        var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName, cancellationToken: cancellationToken);

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
            Id = "commits", // collectionOptions.CollectionName, TODO make configurable
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
        events.NotNull(nameof(events));

        _logger.LogDebug("Append to stream - {streamName}@{expectedRevision}.", streamId, expectedState);

        TransactionalBatch batch = _container.CreateTransactionalBatch(new PartitionKey(streamId));

        try
        {
            Task<TransactionalBatchResponse> appendTask = expectedState switch
            {
                { } when expectedState == StreamState.StreamExists => AppendToExistingStream(streamId, events, cancellationToken),
                { } when expectedState == StreamState.NoStream => AppendToNewStream(streamId, events, batch, cancellationToken),
                _ => AppendToStreamAnyState(streamId, events, batch, cancellationToken)
            };

            _ = await appendTask;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict || ex.Headers["x-ms-substatus"] == "409" || ex.SubStatusCode == 409)
        {
            throw new ConcurrencyException($"Concurrency conflict when appending to stream {streamId}.", ex);
        }
    }

    public async Task AppendToStream(string streamId, IReadOnlyList<EventData> events, StreamRevision expectedRevision, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Append to stream at {expectedRevision}@{streamId}.", expectedRevision, streamId);

        TransactionalBatch batch = _container.CreateTransactionalBatch(new PartitionKey(streamId));

        var transactionalBatchItemRequestOptions = new TransactionalBatchItemRequestOptions
        {
            EnableContentResponseOnWrite = false // Don't return the event data in the response
        };

        ulong revision = expectedRevision;

        // Attempt to read the event at the expected revision
        batch.ReadItem($"{revision}@{streamId}", new TransactionalBatchItemRequestOptions { EnableContentResponseOnWrite = false });

        for (int index = 0; index < events.Count; index++)
        {
            batch.CreateItem(CosmosEvent.FromEventData(streamId, Convert.ToUInt64(++expectedRevision), events[index], _serializer), transactionalBatchItemRequestOptions);
        }

        // There are two concurrency reasons why this may fail:
        // 1. The expected version does not exist
        // 2. The item has been updated meaning that the batch.CreateItem will attempt to write an event number
        //      that already exists
        // We should think about how we can communicate both of these scenarios

        using var batchResponse = await batch.ExecuteAsync(cancellationToken);

        if (!batchResponse.IsSuccessStatusCode)
        {
            // Could indicate the revision of the stream
            throw new ConcurrencyException($"Concurrency conflict when appending to stream {streamId}. Stream revision expectation failed");
        }
    }

    private async Task<TransactionalBatchResponse> AppendToStreamAnyState(string streamId, IReadOnlyList<EventData> events, TransactionalBatch batch, CancellationToken cancellationToken)
    {
        // To append to a stream in any state we need to obtain the last event of the stream
        // This is not particular performant - it may be better to try a point read first to see if the stream exists
        // Then fall back to getting the last event number
        var queryDefinition = new QueryDefinition(@"
            SELECT TOP 1 VALUE e.event_number
            FROM e
            WHERE e.stream_id = @stream_id
            ORDER BY e.event_number DESC" // Do we need to sort or is the default sort enough?
        )
        .WithParameter("@stream_id", streamId);

        var options = new QueryRequestOptions
        {
            MaxItemCount = 1,
            PartitionKey = new PartitionKey(streamId)
        };

        using var eventsQuery = _container.GetItemQueryIterator<ulong>(queryDefinition, requestOptions: options);
        ulong expectedRevision = 0;

        if (eventsQuery.HasMoreResults)
        {
            expectedRevision = (await eventsQuery.ReadNextAsync(cancellationToken)).SingleOrDefault();
        }

        var transactionalBatchItemRequestOptions = new TransactionalBatchItemRequestOptions
        {
            EnableContentResponseOnWrite = false
        };

        // Increment event number based on the current revision
        // This can still fail if the stream is written to in the meantime
        for (int index = 0; index < events.Count; index++)
        {
            batch.CreateItem(CosmosEvent.FromEventData(streamId, Convert.ToUInt64(++expectedRevision), events[index], _serializer), transactionalBatchItemRequestOptions);
        }

        using TransactionalBatchResponse batchResponse = await batch.ExecuteAsync(cancellationToken);

        return batchResponse.IsSuccessStatusCode
            ? batchResponse
            : throw new CosmosException(batchResponse.ErrorMessage, batchResponse.StatusCode, 0,
                batchResponse.ActivityId, batchResponse.RequestCharge);
    }

    private async Task<TransactionalBatchResponse> AppendToNewStream(string streamId, IReadOnlyList<EventData> events, TransactionalBatch batch, CancellationToken cancellationToken)
    {
        var transactionalBatchItemRequestOptions = new TransactionalBatchItemRequestOptions
        {
            EnableContentResponseOnWrite = false // Don't return the event data in the response
        };

        // If 0@{streamId} exists then the stream exists and 
        // the transaction will fail since batch.CreateItem fails if the item exists

        for (int index = 0; index < events.Count; index++)
        {
            batch.CreateItem(CosmosEvent.FromEventData(streamId, Convert.ToUInt64(index), events[index], _serializer), transactionalBatchItemRequestOptions);
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

    private static Task<TransactionalBatchResponse> AppendToExistingStream(string streamId, IEnumerable<EventData> events, CancellationToken cancellationToken)
    {
        // This is the same as AppendToStreamAnyState accept it expects the stream to exist
        throw new NotImplementedException();
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