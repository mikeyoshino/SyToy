using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ToyStore.Application.Characters;
using ToyStore.Application.Characters.SearchCharacters;
using ToyStore.Domain.Catalog;
using ToyStore.Infrastructure;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Application.Characters;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class SearchCharactersQueryTests(PostgreSqlFixture postgreSql)
{
    private static readonly Guid ArchivedUniverseId =
        Guid.Parse("92000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task SearchSupportsBlankNormalizationExactOrderingCapScopeAndUniverseAvailability()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        await SeedSearchDataAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = new SearchCharactersHandler(
            scope.ServiceProvider.GetRequiredService<ICharacterSearchReader>());
        var cancellationToken = TestContext.Current.CancellationToken;

        var blankNull = await handler.Handle(
            new SearchCharactersQuery(CatalogSeedIds.MarvelUniverse),
            cancellationToken);
        var blankEmpty = await handler.Handle(
            new SearchCharactersQuery(CatalogSeedIds.MarvelUniverse, string.Empty),
            cancellationToken);
        var blankWhitespace = await handler.Handle(
            new SearchCharactersQuery(CatalogSeedIds.MarvelUniverse, " \t\r\n"),
            cancellationToken);
        var contains = await handler.Handle(
            new SearchCharactersQuery(CatalogSeedIds.MarvelUniverse, "man"),
            cancellationToken);
        var whitespaceAndWidthExact = await handler.Handle(
            new SearchCharactersQuery(
                CatalogSeedIds.MarvelUniverse,
                "  Ｓｐｉｄｅｒ\u2003  Ｍａｎ "),
            cancellationToken);
        var combiningExact = await handler.Handle(
            new SearchCharactersQuery(CatalogSeedIds.MarvelUniverse, "Ｃａｆé"),
            cancellationToken);
        var capped = await handler.Handle(
            new SearchCharactersQuery(CatalogSeedIds.MarvelUniverse, Limit: 3),
            cancellationToken);
        var otherUniverse = await handler.Handle(
            new SearchCharactersQuery(CatalogSeedIds.DcUniverse, "Spider Man"),
            cancellationToken);
        var zero = await handler.Handle(
            new SearchCharactersQuery(CatalogSeedIds.MarvelUniverse, "ไม่พบ"),
            cancellationToken);
        var archived = await handler.Handle(
            new SearchCharactersQuery(ArchivedUniverseId, "Ghost"),
            cancellationToken);
        var missing = await handler.Handle(
            new SearchCharactersQuery(Guid.Parse("92000000-0000-0000-0000-000000000099")),
            cancellationToken);

        Assert.True(blankNull.IsSuccess);
        Assert.Equal(20, blankNull.Value.Items.Count);
        Assert.False(blankNull.Value.HasExactMatch);
        Assert.Equal(blankNull.Value.Items, blankEmpty.Value.Items);
        Assert.Equal(blankNull.Value.Items, blankWhitespace.Value.Items);
        Assert.Equal(blankNull.Value.HasExactMatch, blankEmpty.Value.HasExactMatch);
        Assert.Equal(blankNull.Value.HasExactMatch, blankWhitespace.Value.HasExactMatch);
        Assert.Equal(
            blankNull.Value.Items.OrderBy(item => Normalize(item.Name)).ThenBy(item => item.Id),
            blankNull.Value.Items);

        Assert.True(contains.IsSuccess);
        Assert.Equal(
            ["Man", "Ant Man", "Iron Man", "Spider Man"],
            contains.Value.Items.Select(item => item.Name));
        Assert.True(contains.Value.HasExactMatch);

        Assert.Equal("Spider Man", Assert.Single(whitespaceAndWidthExact.Value.Items).Name);
        Assert.True(whitespaceAndWidthExact.Value.HasExactMatch);
        Assert.Equal("Cafe\u0301", Assert.Single(combiningExact.Value.Items).Name);
        Assert.True(combiningExact.Value.HasExactMatch);

        Assert.Equal(3, capped.Value.Items.Count);
        Assert.False(capped.Value.HasExactMatch);
        Assert.Equal(CatalogSeedIds.DcUniverse, Assert.Single(otherUniverse.Value.Items).UniverseId);
        Assert.Empty(zero.Value.Items);
        Assert.False(zero.Value.HasExactMatch);
        Assert.Equal(CharacterErrors.UniverseUnavailable, archived.Error);
        Assert.Equal(CharacterErrors.UniverseUnavailable, missing.Error);
    }

    [Fact]
    public async Task ReaderUsesFreshNoTrackingContextsForOverlappingReadsAndPropagatesCancellation()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        await SeedSearchDataAsync(factory);

        var contextFactory = new RecordingContextFactory(postgreSql.ConnectionString);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = postgreSql.ConnectionString,
            })
            .Build());
        services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
        services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(contextFactory);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var scopedContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        scopedContext.Characters.Add(Character.Create(
            Guid.NewGuid(),
            CatalogSeedIds.MarvelUniverse,
            "Unsaved Scoped Character"));
        var contextsBeforeReads = contextFactory.CreatedContexts.ToHashSet();
        var reader = scope.ServiceProvider.GetRequiredService<ICharacterSearchReader>();
        var request = new CharacterSearchReadRequest(
            CatalogSeedIds.MarvelUniverse,
            "SPIDER MAN",
            20);

        var firstRead = reader.ReadAsync(request, TestContext.Current.CancellationToken);
        var secondRead = reader.ReadAsync(request, TestContext.Current.CancellationToken);
        await Task.WhenAll(firstRead, secondRead);
        var readContexts = contextFactory.CreatedContexts
            .Where(context => !contextsBeforeReads.Contains(context))
            .ToArray();

        Assert.Equal(2, readContexts.Length);
        Assert.Equal(2, readContexts.Distinct().Count());
        Assert.All(readContexts, context =>
        {
            Assert.Equal(0, contextFactory.TrackedEntityCount(context));
            Assert.Throws<ObjectDisposedException>(() => context.ChangeTracker.HasChanges());
        });
        Assert.Equal("Spider Man", Assert.Single((await firstRead).Items).Name);
        Assert.Equal((await firstRead).Items, (await secondRead).Items);

        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            reader.ReadAsync(request, cancellationSource.Token));
    }

    private static async Task SeedSearchDataAsync(ToyStoreWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = new DateTimeOffset(2026, 7, 17, 7, 0, 0, TimeSpan.Zero);
        var archivedUniverse = Universe.CreateWithLogo(
            ArchivedUniverseId,
            "จักรวาลเก็บถาวร",
            "Archived Character Universe",
            CatalogSlug.Create("archived-character-universe"),
            CatalogMediaReference.Create(
                "universes/archived-character.webp",
                "/media/universes/archived-character.webp",
                "โลโก้จักรวาลเก็บถาวร"),
            now,
            "character-search-test");
        archivedUniverse.Archive(now.AddMinutes(1), "character-search-test");
        dbContext.Universes.Add(archivedUniverse);

        var characters = new List<Character>
        {
            Character.Create(CharacterId(1), CatalogSeedIds.MarvelUniverse, "Man"),
            Character.Create(CharacterId(2), CatalogSeedIds.MarvelUniverse, "Ant Man"),
            Character.Create(CharacterId(3), CatalogSeedIds.MarvelUniverse, "Iron Man"),
            Character.Create(CharacterId(4), CatalogSeedIds.MarvelUniverse, "Spider Man"),
            Character.Create(CharacterId(5), CatalogSeedIds.MarvelUniverse, "Cafe\u0301"),
            Character.Create(CharacterId(6), CatalogSeedIds.DcUniverse, "Spider Man"),
            Character.Create(CharacterId(7), ArchivedUniverseId, "Ghost"),
        };
        characters.AddRange(Enumerable.Range(1, 25).Select(sequence => Character.Create(
            CharacterId(100 + sequence),
            CatalogSeedIds.MarvelUniverse,
            $"Hero {sequence:00}")));
        dbContext.Characters.AddRange(characters);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static Guid CharacterId(int sequence) =>
        Guid.Parse($"93000000-0000-0000-0000-{sequence:000000000000}");

    private static string Normalize(string value) =>
        CatalogNameNormalizer.Normalize(value);

    private sealed class RecordingContextFactory(string connectionString)
        : IDbContextFactory<ApplicationDbContext>
    {
        private readonly object gate = new();
        private readonly DbContextOptions<ApplicationDbContext> options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connectionString)
                .Options;
        private readonly List<ApplicationDbContext> contexts = [];
        private readonly Dictionary<ApplicationDbContext, int> trackedCounts = [];

        public ApplicationDbContext[] CreatedContexts
        {
            get
            {
                lock (gate)
                {
                    return contexts.ToArray();
                }
            }
        }

        public int TrackedEntityCount(ApplicationDbContext context)
        {
            lock (gate)
            {
                return trackedCounts[context];
            }
        }

        public ApplicationDbContext CreateDbContext()
        {
            var context = new ApplicationDbContext(options);
            lock (gate)
            {
                contexts.Add(context);
                trackedCounts.Add(context, 0);
            }

            context.ChangeTracker.Tracked += (_, _) =>
            {
                lock (gate)
                {
                    trackedCounts[context]++;
                }
            };
            return context;
        }
    }
}
