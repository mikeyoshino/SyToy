using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ToyStore.Application.Common.Files;

namespace ToyStore.Infrastructure.Persistence;

internal sealed partial class MediaCleanupRegistry(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    TimeProvider timeProvider,
    ILogger<MediaCleanupRegistry> logger) : IMediaCleanupRegistry
{
    public async Task RecordAsync(
        MediaCleanupRegistration registration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({registration.StorageKey.Value}, 0))",
                cancellationToken);

            var entry = await context.Set<MediaCleanupEntry>().SingleOrDefaultAsync(
                candidate => candidate.StorageKey == registration.StorageKey.Value
                    && candidate.ResolvedAtUtc == null,
                cancellationToken);
            var observedAtUtc = timeProvider.GetUtcNow();
            if (entry is null)
            {
                context.Add(MediaCleanupEntry.Create(registration, observedAtUtc));
            }
            else
            {
                entry.ObserveAgain(registration, observedAtUtc);
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cleanup bookkeeping must never turn a completed primary mutation into a retry.
        }
        catch (Exception exception)
        {
            LogWriteFailure(logger, registration.EntityType, exception);
        }
    }

    [LoggerMessage(
        EventId = 2100,
        Level = LogLevel.Error,
        Message = "Failed to record a media cleanup entry for {EntityType}.")]
    private static partial void LogWriteFailure(
        ILogger logger,
        string entityType,
        Exception exception);
}
