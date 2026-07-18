using Microsoft.Extensions.Logging.Abstractions;
using ToyStore.Application.Brands;
using ToyStore.Application.Brands.ArchiveBrand;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Catalog;

namespace ToyStore.UnitTests.Application.Brands;

public sealed class ArchiveBrandTests
{
    [Fact]
    public async Task ValidatorRequiresIdentityAndPositiveVersion()
    {
        var result = await new ArchiveBrandValidator().ValidateAsync(
            new ArchiveBrandCommand(Guid.Empty, 0),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(ArchiveBrandCommand.Id)
            && failure.ErrorMessage == "รหัสแบรนด์ไม่ถูกต้อง");
        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(ArchiveBrandCommand.ExpectedVersion)
            && failure.ErrorMessage == "เวอร์ชันข้อมูลแบรนด์ไม่ถูกต้อง");
    }

    [Fact]
    public async Task HappyPathArchivesWithoutMediaStagingAndPreservesImage()
    {
        var brand = Brand.CreateWithImage(
            Guid.NewGuid(),
            "แบรนด์",
            "Brand",
            CatalogSlug.Create("brand"),
            CatalogMediaReference.Create("brand.webp", "/media/brand.webp", "รูปแบรนด์"),
            UtcNow,
            "creator");
        var handler = Handler(brand);
        var command = new ArchiveBrandCommand(brand.Id, brand.Version);

        var result = await AuthorizeAndHandleAsync(handler, command);

        Assert.True(result.IsSuccess);
        Assert.Equal(CatalogReferenceStatus.Archived, brand.Status);
        Assert.Equal(2, brand.Version);
        Assert.Equal("admin-1", brand.ArchivedBy);
        Assert.Equal("brand.webp", brand.Image!.StorageKey);
    }

    [Fact]
    public async Task NotFoundArchivedAndStaleReturnTypedFailuresWithoutMutation()
    {
        var missingId = Guid.NewGuid();
        var missing = await AuthorizeAndHandleAsync(
            Handler(null),
            new ArchiveBrandCommand(missingId, 1));
        Assert.Equal(BrandErrors.NotFound, missing.Error);

        var archivedBrand = Brand.Create(
            Guid.NewGuid(), "เก็บแล้ว", "Archived", CatalogSlug.Create("archived"),
            UtcNow, "creator");
        archivedBrand.Archive(UtcNow.AddMinutes(1), "archiver");
        var archived = await AuthorizeAndHandleAsync(
            Handler(archivedBrand),
            new ArchiveBrandCommand(archivedBrand.Id, archivedBrand.Version));
        Assert.Equal(BrandErrors.Archived, archived.Error);

        var active = Brand.Create(
            Guid.NewGuid(), "กำลังใช้", "Active", CatalogSlug.Create("active"),
            UtcNow, "creator");
        var stale = await AuthorizeAndHandleAsync(
            Handler(active),
            new ArchiveBrandCommand(active.Id, active.Version + 1));
        Assert.Equal(BrandErrors.StaleVersion, stale.Error);
        Assert.Equal(CatalogReferenceStatus.Active, active.Status);
        Assert.Equal(1, active.Version);
    }

    [Fact]
    public void CommandUsesBrandPersistenceMappingWithoutAutomaticTransaction()
    {
        var command = new ArchiveBrandCommand(Guid.NewGuid(), 1);
        var persistence = Assert.IsAssignableFrom<
            IPersistenceFailureResultRequest<Result<BrandMutationResult>>>(command);

        Assert.Equal(
            BrandErrors.StaleVersion,
            persistence.MapPersistenceFailure(new PersistenceFailure(
                PersistenceFailureTarget.Request,
                PersistenceFailureKind.ConcurrencyConflict)));
        Assert.DoesNotContain(
            command.GetType().GetInterfaces(),
            contract => contract.IsGenericType
                && contract.GetGenericTypeDefinition().Name == "ICommand`1");
    }

    private static readonly DateTimeOffset UtcNow =
        new(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);

    private static ArchiveBrandHandler Handler(Brand? brand)
    {
        var factory = new FakeFactory(new FakeSession(brand));
        return new ArchiveBrandHandler(
            factory,
            new CatalogCommitOutcomeResolver(
                NullLogger<CatalogCommitOutcomeResolver>.Instance),
            new FixedTimeProvider());
    }

    private static async Task<Result<BrandMutationResult>> AuthorizeAndHandleAsync(
        ArchiveBrandHandler handler,
        ArchiveBrandCommand command)
    {
        var authorization = new AuthorizationBehavior<ArchiveBrandCommand, Result<BrandMutationResult>>(
            new StubAuthorization());
        return await authorization.Handle(
            command,
            cancellationToken => handler.Handle(command, cancellationToken),
            TestContext.Current.CancellationToken);
    }

    private sealed class FakeFactory(FakeSession session) : IBrandMutationSessionFactory
    {
        public ValueTask<IBrandMutationSession> OpenAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IBrandMutationSession>(session);

        public Task<CatalogCommitVerification<BrandMutationEvidence>> VerifyCommitAsync(
            BrandMutationEvidence evidence,
            CancellationToken cancellationToken) =>
            Task.FromResult(CatalogCommitVerificationResult.Committed(evidence));
    }

    private sealed class FakeSession(Brand? brand) : IBrandMutationSession
    {
        public Task AcquireMutationLockAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Archive must not acquire the name-allocation lock.");

        public Task<Brand?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(brand?.Id == id ? brand : null);

        public Task<bool> DisplayNameExistsAsync(
            string normalizedDisplayName,
            Guid? excludedId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> EnglishNameExistsAsync(
            string normalizedEnglishName,
            Guid? excludedId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CatalogSlug> AllocateSlugAsync(
            string englishName,
            Guid? excludedId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public void Add(Brand value) => throw new NotSupportedException();

        public async Task<CatalogMutationExecution<T>> ExecuteOnceAsync<T>(
            Func<CancellationToken, Task<Result<T>>> operation,
            CancellationToken cancellationToken)
        {
            var result = await operation(cancellationToken);
            return new CatalogMutationExecution<T>(
                result,
                result.IsSuccess
                    ? CatalogCommitOutcome.Committed
                    : CatalogCommitOutcome.DefinitelyRolledBack);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => UtcNow.AddMinutes(2);
    }

    private sealed class StubAuthorization : ICurrentUserAuthorization
    {
        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policyName,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CurrentUserAuthorizationResult(true, true, "admin-1"));
    }
}
