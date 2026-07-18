using System.Text.RegularExpressions;

namespace ToyStore.Web.Diagnostics;

public sealed partial class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var inboundCorrelationId = context.Request.Headers[HeaderName].ToString();
        var correlationId = IsValid(inboundCorrelationId)
            ? inboundCorrelationId
            : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        context.Response.OnStarting(
            static state =>
            {
                var (httpContext, id) = ((HttpContext Context, string Id))state;
                httpContext.Response.Headers[HeaderName] = id;
                return Task.CompletedTask;
            },
            (context, correlationId));

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await next(context);
        }
    }

    private static bool IsValid(string value) =>
        value.Length is >= 1 and <= 128 && CorrelationIdPattern().IsMatch(value);

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CorrelationIdPattern();
}
