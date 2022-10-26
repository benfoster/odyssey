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

    public async Task AppendToStream(string streamId, IEnumerable<EventData> events, StreamState expectedState, CancellationToken cancellationToken = default)
    {
        streamId.NotNullOrWhiteSpace();
        events.NotNull(nameof(events));

        _logger.LogDebug("Append to stream - {streamName}@{expectedRevision}.", streamId, expectedState);

        const int firstEventNumber = 1; // TODO calculate

        try
        {
            var transactionalBatchItemRequestOptions = new TransactionalBatchItemRequestOptions
            {
                EnableContentResponseOnWrite = false
            };

            TransactionalBatch batch = _container.CreateTransactionalBatch(new PartitionKey(streamId));

            foreach (EventData @event in events)
            {
                // TODO calculate event number
                batch.CreateItem(CosmosEvent.FromEventData(streamId, 1, @event, _serializer), transactionalBatchItemRequestOptions);
            }

            Task<TransactionalBatchResponse> appendTask = expectedState switch
            {
                { } when expectedState == StreamState.StreamExists => AppendToExistingStream(streamId, events, cancellationToken),
                { } when expectedState == StreamState.NoStream => AppendToNewStream(streamId, events, cancellationToken),
                _ => AppendToStream(batch, cancellationToken)
            };

            TransactionalBatchResponse batchResponse = await appendTask;

            // var batchResponse = firstEventNumber == 1 ?
            //     await CreateEvents(batch, cancellationToken) :
            //     await CreateEventsOnlyIfPreviousEventExists(batch, streamId, firstEventNumber - 1, cancellationToken);

            // _loggingOptions.OnSuccess(ResponseInformation.FromWriteResponse(nameof(AppendToStream), batchResponse));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict || ex.Headers["x-ms-substatus"] == "409" || ex.SubStatusCode == 409)
        {
            throw new ConcurrencyException(
                $"Concurrency conflict when appending to stream {streamId}. Expected revision {firstEventNumber - 1}", ex);
        }
    }

    public async Task AppendToStream(string streamId, IEnumerable<EventData> events, StreamRevision expectedRevision, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Append to stream - {streamName}@{expectedRevision}.", streamId, expectedRevision);

        await AppendToExistingStream(streamId, events, expectedRevision, cancellationToken);
    }

    private static async Task<TransactionalBatchResponse> AppendToStream(TransactionalBatch batch, CancellationToken cancellationToken)
    {
        using TransactionalBatchResponse batchResponse = await batch.ExecuteAsync(cancellationToken);

        return batchResponse.IsSuccessStatusCode
            ? batchResponse
            : throw new CosmosException(batchResponse.ErrorMessage, batchResponse.StatusCode, 0,
                batchResponse.ActivityId, batchResponse.RequestCharge);
    }

    private static Task<TransactionalBatchResponse> AppendToNewStream(string streamId, IEnumerable<EventData> events, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private static Task<TransactionalBatchResponse> AppendToExistingStream(string streamId, IEnumerable<EventData> events, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private Task<TransactionalBatchResponse> AppendToExistingStream(string streamId, IEnumerable<EventData> events, StreamRevision expectedRevision, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyCollection<EventData>> ReadStream(string streamId, Direction direction, StreamPosition position, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}