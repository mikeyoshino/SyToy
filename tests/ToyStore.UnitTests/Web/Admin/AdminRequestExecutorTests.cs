using Microsoft.Extensions.Logging;
using ToyStore.Application.Common.Diagnostics;
using ToyStore.Application.Common.Models;
using ToyStore.Web.Components.Admin.Primitives;

namespace ToyStore.UnitTests.Web.Admin;

public sealed class AdminRequestExecutorTests
{
    [Fact]
    public async Task MarkedUnexpectedExceptionReturnsSafeThaiFailureWithoutRelogging()
    {
        var logger = new CountingLogger<AdminRequestExecutor>();
        var executor = new AdminRequestExecutor(logger);
        var exception = new InvalidOperationException("application already logged this");
        ExceptionLogOwnership.MarkApplicationLogged(exception);

        var result = await executor.ExecuteAsync<int>(
            _ => Task.FromException<Result<int>>(exception),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("System.Unexpected", result.Error.Code);
        Assert.Equal("เกิดข้อผิดพลาดในระบบ กรุณาลองใหม่อีกครั้ง", result.Error.Message);
        Assert.Equal(0, logger.ErrorCount);
    }

    [Fact]
    public async Task UnmarkedUnexpectedExceptionIsLoggedMarkedAndConvertedOnce()
    {
        var logger = new CountingLogger<AdminRequestExecutor>();
        var executor = new AdminRequestExecutor(logger);
        var exception = new InvalidOperationException("interactive callback failed");

        var result = await executor.ExecuteAsync<int>(
            _ => Task.FromException<Result<int>>(exception),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(1, logger.ErrorCount);
        Assert.True(ExceptionLogOwnership.IsApplicationLogged(exception));
    }

    [Fact]
    public async Task CancellationAlwaysEscapesTheInteractiveBoundary()
    {
        var executor = new AdminRequestExecutor(new CountingLogger<AdminRequestExecutor>());
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync<int>(
            _ => Task.FromCanceled<Result<int>>(source.Token),
            source.Token));
    }

    [Fact]
    public async Task TypedBusinessFailurePassesThroughWithoutLogging()
    {
        var logger = new CountingLogger<AdminRequestExecutor>();
        var executor = new AdminRequestExecutor(logger);
        var expected = Result.Failure(new Error(
            "Brand.DuplicateName",
            "มีชื่อแบรนด์นี้แล้ว",
            ErrorType.Conflict));

        var actual = await executor.ExecuteAsync(
            _ => Task.FromResult(expected),
            CancellationToken.None);

        Assert.Same(expected, actual);
        Assert.Equal(0, logger.ErrorCount);
    }

    private sealed class CountingLogger<T> : ILogger<T>
    {
        public int ErrorCount { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

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
