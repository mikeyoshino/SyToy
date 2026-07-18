using ToyStore.Application.Common.Interfaces;

namespace ToyStore.Web.Identity;

public sealed partial class AdminBootstrapCommand(
    IConfiguration configuration,
    IAdminBootstrapper bootstrapper,
    ILogger<AdminBootstrapCommand> logger)
{
    public static bool IsRequested(IEnumerable<string> arguments) =>
        arguments.Contains("--bootstrap-admin", StringComparer.Ordinal);

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var email = configuration["BootstrapAdmin:Email"];
        var temporaryPassword = configuration["BootstrapAdmin:TemporaryPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(temporaryPassword))
        {
            LogMissingConfiguration(logger);
            return 1;
        }

        var result = await bootstrapper.CreateFirstAdminAsync(
            email,
            temporaryPassword,
            cancellationToken);

        if (result.IsFailure)
        {
            LogBootstrapFailed(logger, result.Error.Code);
            return 1;
        }

        LogBootstrapCompleted(logger, result.Value.UserId);
        return 0;
    }

    [LoggerMessage(
        EventId = 1200,
        Level = LogLevel.Error,
        Message = "First Admin bootstrap requires configured email and temporary password.")]
    private static partial void LogMissingConfiguration(ILogger logger);

    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Error,
        Message = "First Admin bootstrap failed with error code {ErrorCode}.")]
    private static partial void LogBootstrapFailed(ILogger logger, string errorCode);

    [LoggerMessage(
        EventId = 1202,
        Level = LogLevel.Information,
        Message = "First Admin bootstrap completed for user ID {UserId}.")]
    private static partial void LogBootstrapCompleted(ILogger logger, string userId);
}
