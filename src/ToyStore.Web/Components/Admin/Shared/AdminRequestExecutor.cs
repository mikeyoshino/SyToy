using ToyStore.Application.Common.Diagnostics;
using ToyStore.Application.Common.Models;

namespace ToyStore.Web.Components.Admin.Primitives;

public sealed partial class AdminRequestExecutor(ILogger<AdminRequestExecutor> logger)
{
    private static readonly Error UnexpectedError = new(
        "System.Unexpected",
        "เกิดข้อผิดพลาดในระบบ กรุณาลองใหม่อีกครั้ง",
        ErrorType.Failure);

    public async Task<Result<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            return await operation(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogOnce(exception);
            return Result<T>.Failure(UnexpectedError);
        }
    }

    public async Task<Result> ExecuteAsync(
        Func<CancellationToken, Task<Result>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            return await operation(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogOnce(exception);
            return Result.Failure(UnexpectedError);
        }
    }

    private void LogOnce(Exception exception)
    {
        if (ExceptionLogOwnership.IsApplicationLogged(exception))
        {
            return;
        }

        LogInteractiveFailure(logger, exception);
        ExceptionLogOwnership.MarkApplicationLogged(exception);
    }

    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Error,
        Message = "Unhandled exception in an interactive Admin request.")]
    private static partial void LogInteractiveFailure(ILogger logger, Exception exception);
}
