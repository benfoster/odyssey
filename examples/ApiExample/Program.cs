using Microsoft.Azure.Cosmos;
using Minid;
using Odyssey;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

using CosmosClient client = CreateClient(builder.Configuration);
var eventStore = new EventStore(client, "odyssey", app.Services.GetRequiredService<ILoggerFactory>());

await eventStore.Initialize();


app.MapPost("/payments", async (PaymentRequest payment) =>
{
    var initiated = new PaymentInitiated(Id.NewId("pay"), payment.Amount, payment.Currency, payment.Reference);

    await eventStore.AppendToStream(initiated.Id.ToString(), new[] { Map(initiated) }, StreamState.Any);

    return Results.Ok();
});

app.Run();


static EventData Map<TEvent>(TEvent @event)
    => new(Guid.NewGuid(), @event!.GetType().Name, @event);



static CosmosClient CreateClient(IConfiguration configuration)
{
    return new(
        accountEndpoint: configuration["Cosmos:Endpoint"],
        authKeyOrResourceToken: configuration["Cosmos:Token"]
    );
}

record PaymentRequest(int Amount, string Currency, string Reference);
record PaymentInitiated(Id Id, int Amount, string Currency, string Reference);
