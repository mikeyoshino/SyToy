using Microsoft.EntityFrameworkCore;
using ToyStore.Infrastructure.Identity;
using ToyStore.Infrastructure.Persistence;

namespace ToyStore.Web.Startup;

public static partial class DatabaseStartupExtensions
{
    public static async Task ApplyMigrationsAndSeedIdentityAsync(
        this WebApplication application,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);

        await using var scope = application.Services.CreateAsyncScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("ToyStore.DatabaseStartup");

        try
        {
            LogMigrationStarted(logger);
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);

            var identityInitializer =
                scope.ServiceProvider.GetRequiredService<IIdentityInitializer>();
            await identityInitializer.SeedRolesAsync(cancellationToken);
            LogMigrationCompleted(logger);
        }
        catch (Exception exception)
        {
            LogMigrationFailed(logger, exception);
            throw;
        }
    }

    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Information,
        Message = "Applying pending database migrations before startup.")]
    private static partial void LogMigrationStarted(ILogger logger);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Information,
        Message = "Database migrations and required Identity roles are ready.")]
    private static partial void LogMigrationCompleted(ILogger logger);

    [LoggerMessage(
        EventId = 1102,
        Level = LogLevel.Critical,
        Message = "Database migration or Identity role initialization failed during startup.")]
    private static partial void LogMigrationFailed(ILogger logger, Exception exception);
}
