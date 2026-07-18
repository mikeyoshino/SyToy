using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ToyStore.Application.Common.Files;
using ToyStore.Infrastructure.Persistence;

namespace ToyStore.UnitTests.Infrastructure;

public sealed class MediaCleanupRegistryTests
{
    [Fact]
    public async Task LedgerWriteFailureIsLoggedExactlyOnceAndNeverEscapes()
    {
        var failure = new InvalidOperationException("injected cleanup ledger failure");
        var logger = new RecordingLogger<MediaCleanupRegistry>();
        var registry = new MediaCleanupRegistry(
            new ThrowingContextFactory(failure),
            TimeProvider.System,
            logger);

        var exception = await Record.ExceptionAsync(() => registry.RecordAsync(
            Registration(),
            CancellationToken.None));

        Assert.Null(exception);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(failure, entry.Exception);
        Assert.DoesNotContain(
            Registration().StorageKey.Value,
            entry.Message,
            StringComparison.Ordinal);
    }

    private static MediaCleanupRegistration Registration()
    {
        var context = new MediaMutationContext("Brand", Guid.NewGuid(), null);
        var media = new StagedMedia(
            "00112233445566778899aabbccddeeff",
            "00112233445566778899aabbccddeeff/ffeeddccbbaa99887766554433221100.webp",
            "/media/00112233445566778899aabbccddeeff/ffeeddccbbaa99887766554433221100.webp",
            "image/webp",
            3);
        return MediaCleanupRegistration.Create(
            context,
            TrustedMediaStorageKey.From(media),
            MediaCleanupReason.DeleteFailed);
    }

    private sealed class ThrowingContextFactory(Exception failure)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => throw failure;
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}
