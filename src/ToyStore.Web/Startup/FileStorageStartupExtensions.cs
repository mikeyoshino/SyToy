using ToyStore.Application.Common.Files;

namespace ToyStore.Web.Startup;

public static partial class FileStorageStartupExtensions
{
    public static async Task InitializeFileStorageAsync(
        this WebApplication application,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        var logger = application.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("ToyStore.FileStorageStartup");

        try
        {
            LogInitializationStarted(logger);
            var initializer = application.Services.GetRequiredService<IFileStorageInitializer>();
            await initializer.InitializeAsync(cancellationToken);
            LogInitializationCompleted(logger);
        }
        catch (Exception exception)
        {
            LogInitializationFailed(logger, exception);
            throw;
        }
    }

    [LoggerMessage(
        EventId = 1150,
        Level = LogLevel.Information,
        Message = "Initializing persistent local media storage and cleaning stale staging batches.")]
    private static partial void LogInitializationStarted(ILogger logger);

    [LoggerMessage(
        EventId = 1151,
        Level = LogLevel.Information,
        Message = "Persistent local media storage is ready.")]
    private static partial void LogInitializationCompleted(ILogger logger);

    [LoggerMessage(
        EventId = 1152,
        Level = LogLevel.Critical,
        Message = "Persistent local media storage initialization failed during startup.")]
    private static partial void LogInitializationFailed(ILogger logger, Exception exception);
}
