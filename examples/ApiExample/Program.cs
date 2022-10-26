using Microsoft.AspNetCore.Http.Json;
using Microsoft.Azure.Cosmos;
using Minid;
using O9d.Json.Formatting;
using Odyssey;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<JsonOptions>(
    opt => opt.SerializerOptions.PropertyNamingPolicy = new JsonSnakeCaseNamingPolicy());

var app = builder.Build();

using CosmosClient client = CreateClient(builder.Configuration);
var eventStore = new EventStore(client, "odyssey", app.Services.GetRequiredService<ILoggerFactory>());

await eventStore.Initialize();


app.MapPost("/payments", async (PaymentRequest payment) =>
{
    var initiated = new PaymentInitiated(Id.NewId("pay"), payment.Amount, payment.Currency, payment.Reference);

    await eventStore.AppendToStream(initiated.Id.ToString(), new[] { Map(initiated) }, StreamState.Any);

    return Results.Ok(new
    {
        initiated.Id
    });
});

app.MapGet("/events/{id}", async (string id) =>
{
    var events = await eventStore.ReadStream(id, Direction.Forwards, StreamPosition.Start);
    return Results.Ok(events);
});

app.Run();


static EventData Map<TEvent>(TEvent @event)
    => new(Guid.NewGuid(), @event!.GetType().Name.ToSnakeCase(), @event);



static CosmosClient CreateClient(IConfiguration configuration)
{
    return new(
        accountEndpoint: configuration["Cosmos:Endpoint"],
        authKeyOrResourceToken: configuration["Cosmos:Token"]
    );
}

record PaymentRequest(int Amount, string Currency, string Reference);
record PaymentInitiated(Id Id, int Amount, string Currency, string Reference);
