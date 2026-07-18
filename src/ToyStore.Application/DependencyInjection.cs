using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Inventory;
using ToyStore.Application.Products;

namespace ToyStore.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembly = typeof(AssemblyReference).Assembly;

        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient<MediaMutationCoordinator>();
        services.AddTransient<ProductMediaMutationCoordinator>();
        services.AddTransient<CatalogCommitOutcomeResolver>();
        services.AddTransient<InventoryCommitOutcomeResolver>();
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(assembly);
            configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
            configuration.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
            configuration.AddOpenBehavior(typeof(PersistenceErrorMappingBehavior<,>));
            configuration.AddOpenBehavior(typeof(TransactionBehavior<,>));
        });

        return services;
    }
}
