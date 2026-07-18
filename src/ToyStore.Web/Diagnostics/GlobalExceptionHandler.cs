using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ToyStore.Application.Common.Diagnostics;

namespace ToyStore.Web.Diagnostics;

public sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        RequestLogLevelSelector.MarkExceptionHandled(httpContext);

        if (exception is OperationCanceledException)
        {
            httpContext.Response.StatusCode = 499;
            return true;
        }

        if (exception is AntiforgeryValidationException
            || exception is BadHttpRequestException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            httpContext.Response.ContentType = "application/problem+json";
            var invalidRequest = new ProblemDetails
            {
                Type = "urn:toystore:problem:invalid-request",
                Status = StatusCodes.Status400BadRequest,
                Title = "คำขอไม่ถูกต้อง",
                Detail = "กรุณาโหลดหน้าใหม่แล้วลองอีกครั้ง"
            };
            invalidRequest.Extensions["traceId"] = httpContext.TraceIdentifier;
            await JsonSerializer.SerializeAsync(
                httpContext.Response.Body,
                invalidRequest,
                JsonOptions,
                cancellationToken);
            return true;
        }

        if (!ExceptionLogOwnership.IsApplicationLogged(exception))
        {
            LogUnhandledException(logger, exception, httpContext.TraceIdentifier);
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Type = "urn:toystore:problem:unexpected-error",
            Status = StatusCodes.Status500InternalServerError,
            Title = "เกิดข้อผิดพลาดในระบบ",
            Detail = "ไม่สามารถดำเนินการได้ในขณะนี้ กรุณาลองใหม่อีกครั้ง"
        };
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            problemDetails,
            JsonOptions,
            cancellationToken);

        return true;
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Unhandled exception for trace {TraceId}.")]
    private static partial void LogUnhandledException(
        ILogger logger,
        Exception exception,
        string traceId);
}
