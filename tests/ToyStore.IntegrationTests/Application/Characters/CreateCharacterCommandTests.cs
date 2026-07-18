using System.Data.Common;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using ToyStore.Application.Characters;
using ToyStore.Application.Characters.CreateCharacter;
using ToyStore.Application.Characters.SearchCharacters;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Universes;
using ToyStore.Application.Universes.ArchiveUniverse;
using ToyStore.Domain.Catalog;
using ToyStore.Infrastructure;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Application.Characters;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class CreateCharacterCommandTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task CreatedCharacterIsImmediatelyDiscoverableByAuthoritativeNormalizedSearch()
    {
        await using var factory = await StartAndResetAsync();
        var created = await ExecuteCreateAsync(
            factory,
            CatalogSeedIds.MarvelUniverse,
            "  Ｓｐｉｄｅｒ　Ｍａｎ  ");
        Assert.True(created.IsSuccess);

        await using var scope = factory.Services.CreateAsyncScope();
        var searchHandler = new SearchCharactersHandler(
            scope.ServiceProvider.GetRequiredService<ICharacterSearchReader>());
        var searched = await searchHandler.Handle(
            new SearchCharactersQuery(CatalogSeedIds.MarvelUniverse, "Spider Man"),
            TestContext.Current.CancellationToken);

        Assert.True(searched.IsSuccess);
        Assert.True(searched.Value.HasExactMatch);
        Assert.Equal(created.Value, Assert.Single(searched.Value.Items));
    }

    [Fact]
    public async Task ConcurrentEquivalentCreatesInOneUniverseYieldOneTypedDuplicate()
    {
        await using var factory = await StartAndResetAsync();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;

        async Task<Result<CharacterOption>> CreateAsync(string name)
        {
            if (Interlocked.Increment(ref entered) == 2)
            {
                ready.TrySetResult();
            }

            await ready.Task.WaitAsync(TestContext.Current.CancellationToken);
            return await ExecuteCreateAsync(factory, CatalogSeedIds.MarvelUniverse, name);
        }

        var results = await Task.WhenAll(
            CreateAsync("  Spider   Man  "),
            CreateAsync("ＳＰＩＤＥＲ ＭＡＮ"));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Equal(
            CharacterErrors.DuplicateName,
            Assert.Single(results, result => result.IsFailure).Error);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await dbContext.Characters.CountAsync(
            character => character.UniverseId == CatalogSeedIds.MarvelUniverse
                && character.NormalizedName == "SPIDER MAN",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DistinctNamesAndSameNormalizedNameInDifferentUniversesSucceed()
    {
        await using var factory = await StartAndResetAsync();

        var results = await Task.WhenAll(
            ExecuteCreateAsync(factory, CatalogSeedIds.MarvelUniverse, "Hero One"),
            ExecuteCreateAsync(factory, CatalogSeedIds.MarvelUniverse, "Hero Two"),
            ExecuteCreateAsync(factory, CatalogSeedIds.DcUniverse, "Ｈｅｒｏ Ｏｎｅ"));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(3, await dbContext.Characters.CountAsync(
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ArchiveFirstRejectsCreateAndPersistsNoCharacter()
    {
        await using var factory = await StartAndResetAsync();
        var archived = await ExecuteArchiveAsync(factory, CatalogSeedIds.MarvelUniverse, 1);
        Assert.True(archived.IsSuccess);

        var created = await ExecuteCreateAsync(
            factory,
            CatalogSeedIds.MarvelUniverse,
            "Too Late");

        Assert.Equal(CharacterErrors.UniverseUnavailable, created.Error);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await dbContext.Characters.AnyAsync(
            character => character.UniverseId == CatalogSeedIds.MarvelUniverse,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateFirstCommitsBeforeArchiveAndKeepsCharacterReference()
    {
        await using var factory = await StartAndResetAsync();
        var created = await ExecuteCreateAsync(
            factory,
            CatalogSeedIds.MarvelUniverse,
            "Before Archive");
        Assert.True(created.IsSuccess);

        var archived = await ExecuteArchiveAsync(factory, CatalogSeedIds.MarvelUniverse, 1);

        Assert.True(archived.IsSuccess);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await dbContext.Characters.AnyAsync(
            character => character.Id == created.Value.Id,
            TestContext.Current.CancellationToken));
        Assert.Equal(
            CatalogReferenceStatus.Archived,
            await dbContext.Universes
                .Where(universe => universe.Id == CatalogSeedIds.MarvelUniverse)
                .Select(universe => universe.Status)
                .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BarrieredCreateArchiveRaceLinearizesAtUniverseRowLock(
        bool createArrivesFirst)
    {
        var interceptor = new RowLockArrivalInterceptor();
        await using var factory = await StartAndResetAsync();
        await using var provider = CreateBarrierProvider(interceptor);
        var characterHandler = new CreateCharacterHandler(
            provider.GetRequiredService<ICharacterMutationSessionFactory>(),
            new CatalogCommitOutcomeResolver(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<
                    CatalogCommitOutcomeResolver>.Instance));
        var archiveHandler = new ArchiveUniverseHandler(
            provider.GetRequiredService<IUniverseMutationSessionFactory>(),
            new CatalogCommitOutcomeResolver(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<
                    CatalogCommitOutcomeResolver>.Instance),
            provider.GetRequiredService<TimeProvider>());

        Task<Result<CharacterOption>> CreateAsync()
        {
            var command = new CreateCharacterCommand(
                CatalogSeedIds.MarvelUniverse,
                "Barrier Character");
            var authorization = new AuthorizationBehavior<
                CreateCharacterCommand,
                Result<CharacterOption>>(new AdminAuthorization());
            return authorization.Handle(
                command,
                cancellationToken => characterHandler.Handle(command, cancellationToken),
                TestContext.Current.CancellationToken);
        }

        Task<Result<UniverseMutationResult>> ArchiveAsync()
        {
            var command = new ArchiveUniverseCommand(CatalogSeedIds.MarvelUniverse, 1);
            var authorization = new AuthorizationBehavior<
                ArchiveUniverseCommand,
                Result<UniverseMutationResult>>(new AdminAuthorization());
            return authorization.Handle(
                command,
                cancellationToken => archiveHandler.Handle(command, cancellationToken),
                TestContext.Current.CancellationToken);
        }

        Task<Result<CharacterOption>>? createTask = null;
        Task<Result<UniverseMutationResult>>? archiveTask = null;
        if (createArrivesFirst)
        {
            createTask = CreateAsync();
            await interceptor.WaitForArrivalAsync(1, TestContext.Current.CancellationToken);
            archiveTask = ArchiveAsync();
        }
        else
        {
            archiveTask = ArchiveAsync();
            await interceptor.WaitForArrivalAsync(1, TestContext.Current.CancellationToken);
            createTask = CreateAsync();
        }

        interceptor.ReleaseFirstLockHolder();
        var create = await createTask.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
        var archive = await archiveTask.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        Assert.True(archive.IsSuccess);
        if (createArrivesFirst)
        {
            Assert.True(create.IsSuccess);
        }
        else
        {
            Assert.Equal(CharacterErrors.UniverseUnavailable, create.Error);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(
            createArrivesFirst ? 1 : 0,
            await dbContext.Characters.CountAsync(
                character => character.UniverseId == CatalogSeedIds.MarvelUniverse,
                TestContext.Current.CancellationToken));
    }

    private ServiceProvider CreateBarrierProvider(IInterceptor interceptor)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = postgreSql.ConnectionString,
            })
            .Build());
        services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
        services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(
            new InterceptedContextFactory(postgreSql.ConnectionString, interceptor));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task CancellationRollsBackAndReleasesUniverseLockForNextCreate()
    {
        await using var factory = await StartAndResetAsync();
        using var scope = factory.Services.CreateScope();
        var sessionFactory = scope.ServiceProvider
            .GetRequiredService<ICharacterMutationSessionFactory>();
        await using (var session = await sessionFactory.OpenAsync(
            TestContext.Current.CancellationToken))
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                session.ExecuteOnceAsync<CharacterOption>(async cancellationToken =>
                {
                    Assert.True(await session.LockActiveUniverseAsync(
                        CatalogSeedIds.MarvelUniverse,
                        cancellationToken));
                    var character = Character.Create(
                        Guid.NewGuid(),
                        CatalogSeedIds.MarvelUniverse,
                        "Cancelled Character");
                    session.Add(character);
                    throw new OperationCanceledException(cancellationToken);
                }, TestContext.Current.CancellationToken));
        }

        var result = await ExecuteCreateAsync(
                factory,
                CatalogSeedIds.MarvelUniverse,
                "After Cancellation")
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await dbContext.Characters.AnyAsync(
            character => character.Name == "Cancelled Character",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FullMediatRPipelineMapsForcedExactCharacterConstraint()
    {
        await using var factory = new ForcedConstraintFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        await using var scope = factory.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var result = await sender.Send(
            new CreateCharacterCommand(CatalogSeedIds.MarvelUniverse, "Forced Duplicate"),
            TestContext.Current.CancellationToken);

        Assert.Equal(CharacterErrors.DuplicateName, result.Error);
        var forcedFactory = Assert.IsType<ForcedConstraintSessionFactory>(
            scope.ServiceProvider.GetRequiredService<ICharacterMutationSessionFactory>());
        Assert.Equal(1, forcedFactory.ExecutionCount);
    }

    private async Task<ToyStoreWebApplicationFactory> StartAndResetAsync()
    {
        var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        return factory;
    }

    private static async Task<Result<CharacterOption>> ExecuteCreateAsync(
        ToyStoreWebApplicationFactory factory,
        Guid universeId,
        string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<CreateCharacterCommand, Result<CharacterOption>>>();
        var command = new CreateCharacterCommand(universeId, name);
        var authorization = new AuthorizationBehavior<
            CreateCharacterCommand,
            Result<CharacterOption>>(new AdminAuthorization());
        return await authorization.Handle(
            command,
            cancellationToken => handler.Handle(command, cancellationToken),
            TestContext.Current.CancellationToken);
    }

    private static async Task<Result<UniverseMutationResult>> ExecuteArchiveAsync(
        ToyStoreWebApplicationFactory factory,
        Guid universeId,
        long version)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<ArchiveUniverseCommand, Result<UniverseMutationResult>>>();
        var command = new ArchiveUniverseCommand(universeId, version);
        var authorization = new AuthorizationBehavior<
            ArchiveUniverseCommand,
            Result<UniverseMutationResult>>(new AdminAuthorization());
        return await authorization.Handle(
            command,
            cancellationToken => handler.Handle(command, cancellationToken),
            TestContext.Current.CancellationToken);
    }

    private sealed class AdminAuthorization : ICurrentUserAuthorization
    {
        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policyName,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CurrentUserAuthorizationResult(true, true, "admin-1"));
    }

    private sealed class ForcedConstraintFactory(string connectionString)
        : ToyStoreWebApplicationFactory(connectionString)
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICurrentUserAuthorization>();
                services.AddScoped<ICurrentUserAuthorization, AdminAuthorization>();
                services.RemoveAll<ICharacterMutationSessionFactory>();
                services.AddSingleton<ICharacterMutationSessionFactory,
                    ForcedConstraintSessionFactory>();
            });
        }
    }

    private sealed class InterceptedContextFactory(
        string connectionString,
        IInterceptor interceptor) : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(
                    connectionString,
                    npgsql => npgsql.MigrationsAssembly(
                        typeof(ApplicationDbContext).Assembly.FullName))
                .AddInterceptors(interceptor)
                .Options;

        public ApplicationDbContext CreateDbContext() => new(options);
    }

    private sealed class RowLockArrivalInterceptor : DbCommandInterceptor
    {
        private readonly object gate = new();
        private readonly List<TaskCompletionSource> arrivals = [];
        private readonly TaskCompletionSource releaseFirst = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivalCount;

        public Task WaitForArrivalAsync(int expected, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                if (arrivalCount >= expected)
                {
                    return Task.CompletedTask;
                }

                while (arrivals.Count < expected)
                {
                    arrivals.Add(new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously));
                }

                return arrivals[expected - 1].Task.WaitAsync(cancellationToken);
            }
        }

        public void ReleaseFirstLockHolder() => releaseFirst.TrySetResult();

        public override async ValueTask<int> NonQueryExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains(
                    "FROM \"Universes\"",
                    StringComparison.Ordinal)
                && command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal))
            {
                TaskCompletionSource? arrival = null;
                var holdFirst = false;
                lock (gate)
                {
                    arrivalCount++;
                    holdFirst = arrivalCount == 1;
                    if (arrivals.Count >= arrivalCount)
                    {
                        arrival = arrivals[arrivalCount - 1];
                    }
                }

                arrival?.TrySetResult();
                if (holdFirst)
                {
                    await releaseFirst.Task.WaitAsync(cancellationToken);
                }
            }

            return await base.NonQueryExecutedAsync(
                command,
                eventData,
                result,
                cancellationToken);
        }
    }

    private sealed class ForcedConstraintSessionFactory : ICharacterMutationSessionFactory
    {
        public int ExecutionCount { get; private set; }

        public ValueTask<ICharacterMutationSession> OpenAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<ICharacterMutationSession>(new Session(this));

        public Task<CatalogCommitVerification<CharacterMutationEvidence>> VerifyCommitAsync(
            CharacterMutationEvidence evidence,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Forced save failure cannot reach verification.");

        private sealed class Session(ForcedConstraintSessionFactory owner)
            : ICharacterMutationSession
        {
            public Task<bool> LockActiveUniverseAsync(
                Guid universeId,
                CancellationToken cancellationToken) => Task.FromResult(true);

            public Task<bool> NameExistsAsync(
                Guid universeId,
                string normalizedName,
                CancellationToken cancellationToken) => Task.FromResult(false);

            public void Add(Character character)
            {
            }

            public async Task<CatalogMutationExecution<T>> ExecuteOnceAsync<T>(
                Func<CancellationToken, Task<Result<T>>> operation,
                CancellationToken cancellationToken)
            {
                owner.ExecutionCount++;
                var result = await operation(cancellationToken);
                Assert.True(result.IsSuccess);
                throw new DbUpdateException(
                    "forced exact constraint",
                    new PostgresException(
                        "duplicate",
                        "ERROR",
                        "ERROR",
                        PostgresErrorCodes.UniqueViolation,
                        constraintName: "UX_Characters_UniverseId_NormalizedName"));
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
