using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ToyStore.Application.Common.Files;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class MediaCleanupRegistryTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task MissingKeyConcurrentRecordingUsesOneUnresolvedRowAndTracksAttempts()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        var storageKey =
            "11112222333344445555666677778888/9999aaaabbbbccccddddeeeeffff0000.webp";
        Assert.False(File.Exists(Path.Combine(factory.StorageRootPath, "files", storageKey)));
        var registration = Registration("Brand", Guid.NewGuid(), storageKey, MediaCleanupReason.DeleteFailed);
        var registry = factory.Services.GetRequiredService<IMediaCleanupRegistry>();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            registry.RecordAsync(registration, TestContext.Current.CancellationToken)));

        var first = await ReadEntryAsync(storageKey);
        Assert.Equal(1, first.RowCount);
        Assert.Equal(8, first.AttemptCount);
        Assert.Equal("DeleteFailed", first.Reason);
        Assert.Equal("Brand", first.EntityType);
        Assert.Null(first.ResolvedAtUtc);
        Assert.True(first.FirstObservedAtUtc <= first.LastAttemptAtUtc);

        var universeId = Guid.NewGuid();
        await registry.RecordAsync(
            Registration(
                "Universe",
                universeId,
                storageKey,
                MediaCleanupReason.ReferenceVerificationUnavailable),
            TestContext.Current.CancellationToken);

        var repeated = await ReadEntryAsync(storageKey);
        Assert.Equal(1, repeated.RowCount);
        Assert.Equal(9, repeated.AttemptCount);
        Assert.Equal("ReferenceVerificationUnavailable", repeated.Reason);
        Assert.Equal("Universe", repeated.EntityType);
        Assert.Equal(universeId, repeated.EntityId);
        Assert.Equal(first.FirstObservedAtUtc, repeated.FirstObservedAtUtc);
        Assert.True(repeated.LastAttemptAtUtc >= first.LastAttemptAtUtc);
    }

    private async Task<CleanupEntry> ReadEntryAsync(string storageKey)
    {
        await using var connection = new NpgsqlConnection(postgreSql.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) OVER (), "Reason", "EntityType", "EntityId",
                   "FirstObservedAtUtc", "LastAttemptAtUtc", "AttemptCount", "ResolvedAtUtc"
            FROM "MediaCleanupEntries"
            WHERE "StorageKey" = @storageKey AND "ResolvedAtUtc" IS NULL;
            """;
        command.Parameters.AddWithValue("storageKey", storageKey);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        return new CleanupEntry(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetGuid(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetFieldValue<DateTimeOffset>(5),
            reader.GetInt32(6),
            reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7));
    }

    private static MediaCleanupRegistration Registration(
        string entityType,
        Guid entityId,
        string storageKey,
        MediaCleanupReason reason)
    {
        var media = new StagedMedia(
            storageKey[..32],
            storageKey,
            $"/media/{storageKey}",
            "image/webp",
            3);
        return MediaCleanupRegistration.Create(
            new MediaMutationContext(entityType, entityId, null),
            TrustedMediaStorageKey.From(media),
            reason);
    }

    private sealed record CleanupEntry(
        long RowCount,
        string Reason,
        string EntityType,
        Guid EntityId,
        DateTimeOffset FirstObservedAtUtc,
        DateTimeOffset LastAttemptAtUtc,
        int AttemptCount,
        DateTimeOffset? ResolvedAtUtc);
}
