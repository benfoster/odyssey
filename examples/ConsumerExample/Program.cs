using ConsumerExample;
using MediatR;
using Microsoft.Azure.Cosmos;

var builder = Host.CreateDefaultBuilder(args);
builder.ConfigureServices((context, services) =>
{
    CosmosClient client = CreateClient(context.Configuration);
    services.AddSingleton(client);
    services.AddHostedService<EventConsumer>();

    services.AddMediatR(typeof(Program).Assembly);
});

IHost host = builder.Build();
await host.RunAsync();


static CosmosClient CreateClient(IConfiguration configuration)
{
    return new(
        accountEndpoint: configuration["Cosmos:Endpoint"],
        authKeyOrResourceToken: configuration["Cosmos:Token"]
    );
}