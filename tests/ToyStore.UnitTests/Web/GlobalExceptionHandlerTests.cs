using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ToyStore.Application.Common.Diagnostics;
using ToyStore.Web.Diagnostics;

namespace ToyStore.UnitTests.Web;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsyncReturnsSafeThaiProblemDetails()
    {
        const string sentinel = "SENTINEL-secret-value";
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-123"
        };
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException($"Do not expose {sentinel}"),
            TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType, StringComparison.Ordinal);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(
            context.Response.Body,
            cancellationToken: TestContext.Current.CancellationToken);
        var root = document.RootElement;
        Assert.Equal("urn:toystore:problem:unexpected-error", root.GetProperty("type").GetString());
        Assert.Equal("เกิดข้อผิดพลาดในระบบ", root.GetProperty("title").GetString());
        Assert.Equal("ไม่สามารถดำเนินการได้ในขณะนี้ กรุณาลองใหม่อีกครั้ง", root.GetProperty("detail").GetString());
        Assert.Equal(StatusCodes.Status500InternalServerError, root.GetProperty("status").GetInt32());
        Assert.Equal("trace-123", root.GetProperty("traceId").GetString());

        var body = root.GetRawText();
        Assert.DoesNotContain(sentinel, body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), body, StringComparison.Ordinal);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplicationLoggedExceptionIsNotErrorLoggedAgain()
    {
        var context = CreateContext();
        var logger = new CountingLogger<GlobalExceptionHandler>();
        var exception = new InvalidOperationException("handler failed");
        ExceptionLogOwnership.MarkApplicationLogged(exception);
        var handler = new GlobalExceptionHandler(logger);

        await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        Assert.Equal(0, logger.ErrorCount);
    }

    [Fact]
    public async Task UnmarkedHttpExceptionIsErrorLoggedOnce()
    {
        var context = CreateContext();
        var logger = new CountingLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(logger);

        await handler.TryHandleAsync(
            context,
            new InvalidOperationException("endpoint failed"),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, logger.ErrorCount);
    }

    [Fact]
    public async Task CancellationIsNeverErrorLogged()
    {
        var context = CreateContext();
        var logger = new CountingLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(logger);

        await handler.TryHandleAsync(
            context,
            new OperationCanceledException("request cancelled"),
            CancellationToken.None);

        Assert.Equal(0, logger.ErrorCount);
        Assert.Equal(499, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-logging"
        };
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class CountingLogger<T> : ILogger<T>
    {
        public int ErrorCount { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                ErrorCount++;
            }
        }
    }
}
