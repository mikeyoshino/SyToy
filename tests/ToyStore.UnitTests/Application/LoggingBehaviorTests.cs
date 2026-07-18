using MediatR;
using Microsoft.Extensions.Logging;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Diagnostics;
using ToyStore.Application.Common.Models;

namespace ToyStore.UnitTests.Application;

public sealed class LoggingBehaviorTests
{
    private const string SentinelSecret = "never-log-this-secret";

    [Fact]
    public async Task SuccessfulRequestLogsNameAndElapsedWithoutRequestPayload()
    {
        var logger = new CapturingLogger<SecretRequest>();
        var behavior = new LoggingBehavior<SecretRequest, string>(logger);

        var response = await behavior.Handle(
            new SecretRequest(SentinelSecret),
            _ => Task.FromResult("handled"),
            CancellationToken.None);

        Assert.Equal("handled", response);
        Assert.Contains(
            logger.Entries,
            entry => HasProperty(entry, "RequestName", nameof(SecretRequest)));
        Assert.Contains(
            logger.Entries,
            entry => entry.Properties.TryGetValue("ElapsedMilliseconds", out var elapsed)
                && elapsed is double milliseconds
                && milliseconds >= 0);
        Assert.DoesNotContain(logger.Entries, ContainsSentinel);
    }

    [Fact]
    public async Task CancellationNeverProducesAnErrorLog()
    {
        var logger = new CapturingLogger<SecretRequest>();
        var behavior = new LoggingBehavior<SecretRequest, string>(logger);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => behavior.Handle(
            new SecretRequest(SentinelSecret),
            _ => Task.FromCanceled<string>(source.Token),
            source.Token));

        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task TypedFailureCompletesWithoutAnErrorLog()
    {
        var logger = new CapturingLogger<SecretRequest>();
        var behavior = new LoggingBehavior<SecretRequest, Result>(logger);
        var expected = Result.Failure(RequestErrors.Forbidden);

        var result = await behavior.Handle(
            new SecretRequest(SentinelSecret),
            _ => Task.FromResult(expected),
            CancellationToken.None);

        Assert.Same(expected, result);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task FailedRequestLogsFailureWithoutPayloadAndRethrowsException()
    {
        var logger = new CapturingLogger<SecretRequest>();
        var behavior = new LoggingBehavior<SecretRequest, string>(logger);
        var expectedException = new InvalidOperationException("handler failed");

        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => behavior.Handle(
                new SecretRequest(SentinelSecret),
                _ => Task.FromException<string>(expectedException),
                CancellationToken.None));

        Assert.Same(expectedException, actualException);
        var failure = Assert.Single(
            logger.Entries,
            entry => entry.Level == LogLevel.Error);
        Assert.Same(expectedException, failure.Exception);
        Assert.True(HasProperty(failure, "RequestName", nameof(SecretRequest)));
        Assert.True(failure.Properties.ContainsKey("ElapsedMilliseconds"));
        Assert.DoesNotContain(logger.Entries, ContainsSentinel);
        Assert.True(ExceptionLogOwnership.IsApplicationLogged(expectedException));
    }

    private static bool HasProperty(LogEntry entry, string name, object expected) =>
        entry.Properties.TryGetValue(name, out var actual) && Equals(expected, actual);

    private static bool ContainsSentinel(LogEntry entry) =>
        entry.Message.Contains(SentinelSecret, StringComparison.Ordinal)
        || entry.Properties.Values.Any(
            value => value?.ToString()?.Contains(SentinelSecret, StringComparison.Ordinal) == true);

    private sealed record SecretRequest(string Secret) : IRequest<string>;

    private sealed record LogEntry(
        LogLevel Level,
        Exception? Exception,
        string Message,
        IReadOnlyDictionary<string, object?> Properties);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

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
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(value => value.Key, value => value.Value)
                : new Dictionary<string, object?>();

            Entries.Add(new LogEntry(logLevel, exception, formatter(state, exception), properties));
        }
    }
}
