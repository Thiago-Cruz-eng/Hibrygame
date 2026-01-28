using Orchestrator.Infra.BaseRepository;
using Orchestrator.Infra.Mongo;
using Polly;

namespace Orchestrator.Infra.Utils;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddShared(this IServiceCollection services)
    {
        // Domain events via MediatR
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly)
        );
        
        // Shared service registrations
        services.AddTransient<IGenericRepository, GenericRepository>();
        services.AddScoped<IServiceFactory, ServiceFactory>();
        services.AddTransient(typeof(ServiceInstanceResolver<>));
        return services;
    }
}