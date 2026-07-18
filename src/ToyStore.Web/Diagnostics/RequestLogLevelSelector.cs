using Serilog.Events;

namespace ToyStore.Web.Diagnostics;

public static class RequestLogLevelSelector
{
    private static readonly object HandledExceptionKey = new();

    public static LogEventLevel Select(
        HttpContext httpContext,
        double elapsedMilliseconds,
        Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (WasExceptionHandled(httpContext))
        {
            return LogEventLevel.Information;
        }

        if (exception is not null)
        {
            // This is a pipeline-composition fallback. With request diagnostics outside
            // GlobalExceptionHandler, handled exceptions reach this selector as null.
            return exception is OperationCanceledException
                ? LogEventLevel.Information
                : LogEventLevel.Error;
        }

        return httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError
            ? LogEventLevel.Error
            : LogEventLevel.Information;
    }

    internal static void MarkExceptionHandled(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        httpContext.Items[HandledExceptionKey] = true;
    }

    private static bool WasExceptionHandled(HttpContext httpContext) =>
        httpContext.Items.ContainsKey(HandledExceptionKey);
}
