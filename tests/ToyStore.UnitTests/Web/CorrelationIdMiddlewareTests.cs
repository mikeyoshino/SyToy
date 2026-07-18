using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using ToyStore.Web.Diagnostics;

namespace ToyStore.UnitTests.Web;

public sealed class CorrelationIdMiddlewareTests
{
    [Theory]
    [InlineData("request-123")]
    [InlineData("A_b.C-9")]
    public async Task InvokeAsyncRetainsValidInboundCorrelationId(string correlationId)
    {
        var context = CreateContext(correlationId);
        var nextCalls = 0;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        }, new CapturingLogger<CorrelationIdMiddleware>());

        await middleware.InvokeAsync(context);

        Assert.Equal(correlationId, context.TraceIdentifier);
        Assert.Equal(correlationId, context.Response.Headers["X-Correlation-ID"]);
        Assert.Equal(1, nextCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("contains space")]
    [InlineData("line\r\ninjection")]
    [InlineData("ข้อมูลลับ")]
    public async Task InvokeAsyncGeneratesSafeIdForMissingOrInvalidInput(string? correlationId)
    {
        var context = CreateContext(correlationId);
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, new CapturingLogger<CorrelationIdMiddleware>());

        await middleware.InvokeAsync(context);

        Assert.Matches("^[A-Za-z0-9._-]{1,128}$", context.TraceIdentifier);
        Assert.Equal(context.TraceIdentifier, context.Response.Headers["X-Correlation-ID"]);
        Assert.NotEqual(correlationId, context.TraceIdentifier);
    }

    [Fact]
    public async Task InvokeAsyncAcceptsBoundaryLength()
    {
        var correlationId = new string('a', 128);
        var context = CreateContext(correlationId);
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, new CapturingLogger<CorrelationIdMiddleware>());

        await middleware.InvokeAsync(context);

        Assert.Equal(correlationId, context.TraceIdentifier);
    }

    [Fact]
    public async Task InvokeAsyncDoesNotLogRejectedRawInput()
    {
        const string sentinel = "secret\r\nFORGED";
        var logger = new CapturingLogger<CorrelationIdMiddleware>();
        var context = CreateContext(sentinel);
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, logger);

        await middleware.InvokeAsync(context);

        Assert.DoesNotContain(logger.Messages, message => message.Contains(sentinel, StringComparison.Ordinal));
        var scope = Assert.Single(logger.Scopes);
        Assert.True(scope.TryGetValue("CorrelationId", out var scopedCorrelationId));
        Assert.Equal(context.TraceIdentifier, scopedCorrelationId);
        Assert.DoesNotContain(sentinel, scopedCorrelationId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsyncRejectsOverMaximumLength()
    {
        var correlationId = new string('a', 129);
        var context = CreateContext(correlationId);
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, new CapturingLogger<CorrelationIdMiddleware>());

        await middleware.InvokeAsync(context);

        Assert.NotEqual(correlationId, context.TraceIdentifier);
        Assert.Equal(32, context.TraceIdentifier.Length);
    }

    [Fact]
    public async Task InvokeAsyncRejectsMultipleHeaderValues()
    {
        var context = CreateContext(null);
        context.Request.Headers["X-Correlation-ID"] = new StringValues(["valid-one", "valid-two"]);
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, new CapturingLogger<CorrelationIdMiddleware>());

        await middleware.InvokeAsync(context);

        Assert.NotEqual("valid-one", context.TraceIdentifier);
        Assert.NotEqual("valid-two", context.TraceIdentifier);
        Assert.Equal(32, context.TraceIdentifier.Length);
    }

    [Fact]
    public async Task InvokeAsyncRestoresHeaderWhenDownstreamClearsResponse()
    {
        const string correlationId = "request-survives-error";
        var context = CreateContext(correlationId);
        var responseFeature = new StartingResponseFeature();
        context.Features.Set<IHttpResponseFeature>(responseFeature);
        var middleware = new CorrelationIdMiddleware(async currentContext =>
        {
            currentContext.Response.Headers.Clear();
            currentContext.Response.Headers["X-Correlation-ID"] = "overwritten";
            currentContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await responseFeature.FireOnStartingAsync();
        }, new CapturingLogger<CorrelationIdMiddleware>());

        await middleware.InvokeAsync(context);

        Assert.Equal(correlationId, context.Response.Headers["X-Correlation-ID"]);
    }

    private static DefaultHttpContext CreateContext(string? correlationId)
    {
        var context = new DefaultHttpContext();
        if (correlationId is not null)
        {
            context.Request.Headers["X-Correlation-ID"] = correlationId;
        }

        return context;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public List<IReadOnlyDictionary<string, string>> Scopes { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            var values = state is IEnumerable<KeyValuePair<string, object>> properties
                ? properties.ToDictionary(pair => pair.Key, pair => pair.Value.ToString() ?? string.Empty)
                : new Dictionary<string, string>();
            Scopes.Add(values);
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed class StartingResponseFeature : IHttpResponseFeature
    {
        private readonly Stack<(Func<object, Task> Callback, object State)> onStarting = new();

        public int StatusCode { get; set; } = StatusCodes.Status200OK;

        public string? ReasonPhrase { get; set; }

        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public Stream Body { get; set; } = Stream.Null;

        public bool HasStarted { get; private set; }

        public void OnStarting(Func<object, Task> callback, object state) =>
            onStarting.Push((callback, state));

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public async Task FireOnStartingAsync()
        {
            while (onStarting.TryPop(out var registration))
            {
                await registration.Callback(registration.State);
            }

            HasStarted = true;
        }
    }
}
