using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using ToyStore.Application.Common.Diagnostics;

namespace ToyStore.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<TRequest> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly Action<ILogger, string, Exception?> RequestStarted =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, nameof(RequestStarted)),
            "Handling application request {RequestName}");

    private static readonly Action<ILogger, string, double, Exception?> RequestCompleted =
        LoggerMessage.Define<string, double>(
            LogLevel.Information,
            new EventId(2, nameof(RequestCompleted)),
            "Completed application request {RequestName} in {ElapsedMilliseconds} ms");

    private static readonly Action<ILogger, string, double, Exception?> RequestFailed =
        LoggerMessage.Define<string, double>(
            LogLevel.Error,
            new EventId(3, nameof(RequestFailed)),
            "Application request {RequestName} failed after {ElapsedMilliseconds} ms");

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        var requestName = typeof(TRequest).Name;
        var startedAt = Stopwatch.GetTimestamp();
        RequestStarted(logger, requestName, null);

        try
        {
            var response = await next(cancellationToken).ConfigureAwait(false);
            RequestCompleted(logger, requestName, GetElapsedMilliseconds(startedAt), null);
            return response;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (!ExceptionLogOwnership.IsApplicationLogged(exception))
            {
                ExceptionLogOwnership.MarkApplicationLogged(exception);
                RequestFailed(logger, requestName, GetElapsedMilliseconds(startedAt), exception);
            }

            throw;
        }
    }

    private static double GetElapsedMilliseconds(long startedAt) =>
        Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
}
