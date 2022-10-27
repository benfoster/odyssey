namespace ConsumerExample;

using System.Collections.Generic;
using MediatR;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Odyssey;

public class EventConsumer : IHostedService
{
    private readonly CosmosClient _cosmosClient;
    private readonly IMediator _mediator;
    private readonly ILogger<EventConsumer> _logger;
    private ChangeFeedProcessor? _changeFeedProcessor;
    private readonly string _databaseName = "odyssey";

    private readonly JsonSerializer _serializer = JsonSerializer.Create(SerializerSettings.Default);

    private static readonly Dictionary<string, Type> TypeMap = new()
    {
        { "payment_initiated" , typeof(PaymentInitiated)},
        { "UboAdded" , typeof(UboAdded)}
    };

    public EventConsumer(CosmosClient cosmosClient, IMediator mediator, ILogger<EventConsumer> logger)
    {
        _cosmosClient = cosmosClient;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Event Consumer");

        var database = _cosmosClient.GetDatabase(_databaseName);

        // Container reference with creation if it does not alredy exist
        var leaseContainer = await database.CreateContainerIfNotExistsAsync(
            id: "leases",
            partitionKeyPath: "/id", // lease partition ley must be id or partitionKey
            cancellationToken: cancellationToken
        );

        string instanceName = $"{Environment.MachineName}:{Environment.ProcessId}";
        string proecssorName = "payment-events-consumer";

        _logger.LogInformation("Starting Change Feed Processor {Instance}@{ProcessorName}", instanceName, proecssorName);

        // Ref https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/change-feed-processor?tabs=dotnet
        _changeFeedProcessor = _cosmosClient.GetContainer(_databaseName, "events")
            .GetChangeFeedProcessorBuilder<CosmosEvent>(processorName: proecssorName, onChangesDelegate: HandleChangesAsync)
                .WithLeaseAcquireNotification(OnLeaseAcquiredAsync)
                .WithLeaseReleaseNotification(OnLeaseReleaseAsync)
                .WithErrorNotification(OnErrorAsync)
                .WithInstanceName(instanceName)
                .WithLeaseContainer(leaseContainer)
                //.WithStartTime(DateTime.MinValue.ToUniversalTime()) // read from beginning
                .Build();

        await _changeFeedProcessor.StartAsync();
    }

    private Task OnErrorAsync(string leaseToken, Exception exception)
    {
        if (exception is ChangeFeedProcessorUserException userException)
        {
            _logger.LogError(userException, $"Lease {leaseToken} processing failed with unhandled exception from user delegate");
        }
        else
        {
            _logger.LogError(exception, $"Lease {leaseToken} failed");
        }

        return Task.CompletedTask;
    }

    private Task OnLeaseReleaseAsync(string leaseToken)
    {
        _logger.LogInformation($"Lease {leaseToken} is released and processing is stopped");
        return Task.CompletedTask;
    }

    private Task OnLeaseAcquiredAsync(string leaseToken)
    {
        _logger.LogInformation($"Lease {leaseToken} is acquired and will start processing");
        return Task.CompletedTask;
    }

    private async Task HandleChangesAsync(IReadOnlyCollection<CosmosEvent> changes, CancellationToken cancellationToken)
    {
        if (!cancellationToken.IsCancellationRequested) // What can trigger this cancellation token?
        {
            _logger.LogInformation("Receieved {EventCount} events", changes.Count);

            foreach (var @event in changes)
            {
                _logger.LogInformation("Received event {EventType} {EventId}", @event.EventType, @event.Id);

                if (TypeMap.TryGetValue(@event.EventType, out Type? eventType))
                {
                    var typedEvent = @event.Data.ToObject(eventType, _serializer);
                    if (typedEvent != null)
                    {
                        await _mediator.Publish(typedEvent, cancellationToken);
                    }
                }
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Event Consumer");

        if (_changeFeedProcessor != null)
        {
            await _changeFeedProcessor.StopAsync();
        }
    }

    // Container.ChangeFeedMonitorLeaseAcquireDelegate onLeaseAcquiredAsync = (string leaseToken) =>
    // {
    //     _logger.LogInformation($"Lease {leaseToken} is acquired and will start processing");
    //     return Task.CompletedTask;
    // };

    // Container.ChangeFeedMonitorLeaseReleaseDelegate onLeaseReleaseAsync = (string leaseToken) =>
    // {
    //     Console.WriteLine($"Lease {leaseToken} is released and processing is stopped");
    //     return Task.CompletedTask;
    // };

    // Container.ChangeFeedMonitorErrorDelegate onErrorAsync = (string LeaseToken, Exception exception) =>
    // {
    //     if (exception is ChangeFeedProcessorUserException userException)
    //     {
    //         Console.WriteLine($"Lease {LeaseToken} processing failed with unhandled exception from user delegate {userException.InnerException}");
    //     }
    //     else
    //     {
    //         Console.WriteLine($"Lease {LeaseToken} failed with {exception}");
    //     }

    //     return Task.CompletedTask;
    // };
}
