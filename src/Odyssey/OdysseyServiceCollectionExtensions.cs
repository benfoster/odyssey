namespace Odyssey;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using O9d.Guard;

public static class OdysseyServiceCollectionExtensions
{
    public static IServiceCollection AddOdyssey(this IServiceCollection services,
        Action<CosmosEventStoreOptions>? configureOptions = null,
        Func<IServiceProvider, CosmosClient>? cosmosClientFactory = null)
    {
        services.NotNull();

        if (cosmosClientFactory != null && !IsServiceRegistered<CosmosClient>(services))
        {
            services.AddSingleton(cosmosClientFactory);
        }

        configureOptions ??= (_ => { });

        services.Configure(configureOptions);
        services.AddSingleton<IEventStore, CosmosEventStore>();

        return services;
    }

    private static bool IsServiceRegistered<TService>(IServiceCollection services)
        => services.Any(sd => sd.ServiceType == typeof(TService));
}
