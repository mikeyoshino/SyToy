using FluentValidation;
using ToyStore.Application.Characters;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Catalog;

namespace ToyStore.UnitTests.Application.Characters;

public sealed class CharacterContractsTests
{
    [Fact]
    public void ErrorsAndResultModelsHaveStableThaiImmutableContracts()
    {
        Assert.Equal(
            new Error(
                "Character.DuplicateName",
                "ชื่อตัวละครนี้มีอยู่แล้วในจักรวาลที่เลือก",
                ErrorType.Conflict),
            CharacterErrors.DuplicateName);
        Assert.Equal(
            new Error(
                "Character.UniverseUnavailable",
                "ไม่พบจักรวาลที่เลือกหรือจักรวาลนี้ถูกเก็บถาวรแล้ว",
                ErrorType.Conflict),
            CharacterErrors.UniverseUnavailable);

        var universeId = Guid.NewGuid();
        var mutableItems = new List<CharacterOption>
        {
            new(Guid.NewGuid(), universeId, "Iron Man"),
        };
        var result = new SearchCharactersResult(mutableItems, hasExactMatch: true);
        mutableItems.Clear();

        var option = Assert.Single(result.Items);
        Assert.Equal(universeId, option.UniverseId);
        Assert.Equal("Iron Man", option.Name);
        Assert.True(result.HasExactMatch);
    }

    [Fact]
    public void MutationRequestMapsOnlyTheExactCharacterDuplicateAndUsesNoAutomaticTransaction()
    {
        var request = new TestMutationRequest();
        var persistenceRequest = Assert.IsAssignableFrom<
            IPersistenceFailureResultRequest<Result<CharacterOption>>>(request);

        Assert.Equal(PolicyNames.CanManageProducts, request.RequiredPolicy);
        Assert.Equal(
            CharacterErrors.DuplicateName,
            persistenceRequest.MapPersistenceFailure(new PersistenceFailure(
                PersistenceFailureTarget.Character,
                PersistenceFailureKind.DuplicateName)));
        Assert.Null(persistenceRequest.MapPersistenceFailure(new PersistenceFailure(
            PersistenceFailureTarget.Brand,
            PersistenceFailureKind.DuplicateDisplayName)));
        Assert.DoesNotContain(
            request.GetType().GetInterfaces(),
            contract => contract.IsGenericType
                && contract.GetGenericTypeDefinition().Name == "ICommand`1");
    }

    [Fact]
    public void SearchRequestUsesProductPolicyWithoutPersistenceMapping()
    {
        var request = new TestSearchRequest();

        Assert.Equal(PolicyNames.CanManageProducts, request.RequiredPolicy);
        Assert.IsNotAssignableFrom<
            IPersistenceFailureResultRequest<Result<SearchCharactersResult>>>(request);
    }

    [Fact]
    public async Task AnonymousSearchShortCircuitsBeforeReader()
    {
        var request = new TestSearchRequest();
        var reader = new CountingReader();
        var validator = new CountingSearchValidator();
        var validation = new ValidationBehavior<
            TestSearchRequest,
            Result<SearchCharactersResult>>([validator]);
        var behavior = new AuthorizationBehavior<
            TestSearchRequest,
            Result<SearchCharactersResult>>(new StubAuthorization(
                new CurrentUserAuthorizationResult(false, false, null)));

        var result = await behavior.Handle(
            request,
            cancellationToken => validation.Handle(
                request,
                async handlerCancellationToken =>
                {
                    var read = await reader.ReadAsync(
                        new CharacterSearchReadRequest(Guid.NewGuid(), string.Empty, 20),
                        handlerCancellationToken);
                    return Result<SearchCharactersResult>.Success(
                        new SearchCharactersResult(read.Items, read.HasExactMatch));
                },
                cancellationToken),
            CancellationToken.None);

        Assert.Equal(RequestErrors.Unauthorized, result.Error);
        Assert.Equal(0, validator.CallCount);
        Assert.Equal(0, reader.CallCount);
        Assert.Null(request.AuthorizedActorId);
    }

    [Fact]
    public async Task ForbiddenMutationShortCircuitsBeforeSessionFactory()
    {
        var request = new TestMutationRequest();
        var factory = new CountingMutationSessionFactory();
        var behavior = new AuthorizationBehavior<
            TestMutationRequest,
            Result<CharacterOption>>(new StubAuthorization(
                new CurrentUserAuthorizationResult(true, false, "customer-1")));

        var result = await behavior.Handle(
            request,
            async cancellationToken =>
            {
                await using var session = await factory.OpenAsync(cancellationToken);
                return Result<CharacterOption>.Success(
                    new CharacterOption(Guid.NewGuid(), Guid.NewGuid(), "Batman"));
            },
            CancellationToken.None);

        Assert.Equal(RequestErrors.Forbidden, result.Error);
        Assert.Equal(0, factory.OpenCount);
        Assert.Null(request.AuthorizedActorId);
    }

    [Fact]
    public void MutationEvidenceCapturesOnlyImmutableCharacterIdentity()
    {
        var character = Character.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "  Ｉron\u2003  Man  ");

        var evidence = CharacterMutationEvidence.Capture(character);

        Assert.Equal(character.Id, evidence.Id);
        Assert.Equal(character.UniverseId, evidence.UniverseId);
        Assert.Equal("Ｉron\u2003  Man", evidence.Name);
        Assert.Equal("IRON MAN", evidence.NormalizedName);
    }

    private sealed record TestSearchRequest
        : AuthorizedCharacterRequest<Result<SearchCharactersResult>>
    {
        public override Result<SearchCharactersResult> CreateFailure(
            Error requestError,
            IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
            Result<SearchCharactersResult>.Failure(requestError, validationFailures);
    }

    private sealed record TestMutationRequest
        : AuthorizedCharacterMutationRequest<Result<CharacterOption>>
    {
        public override Result<CharacterOption> CreateFailure(
            Error requestError,
            IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
            Result<CharacterOption>.Failure(requestError, validationFailures);
    }

    private sealed class StubAuthorization(CurrentUserAuthorizationResult result)
        : ICurrentUserAuthorization
    {
        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policyName,
            CancellationToken cancellationToken)
        {
            Assert.Equal(PolicyNames.CanManageProducts, policyName);
            return Task.FromResult(result);
        }
    }

    private sealed class CountingReader : ICharacterSearchReader
    {
        public int CallCount { get; private set; }

        public Task<CharacterSearchReadResult> ReadAsync(
            CharacterSearchReadRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new CharacterSearchReadResult(
                universeAvailable: true,
                items: [],
                hasExactMatch: false));
        }
    }

    private sealed class CountingSearchValidator : AbstractValidator<TestSearchRequest>
    {
        public CountingSearchValidator()
        {
            RuleFor(request => request).Custom((_, _) => CallCount++);
        }

        public int CallCount { get; private set; }
    }

    private sealed class CountingMutationSessionFactory : ICharacterMutationSessionFactory
    {
        public int OpenCount { get; private set; }

        public ValueTask<ICharacterMutationSession> OpenAsync(CancellationToken cancellationToken)
        {
            OpenCount++;
            throw new InvalidOperationException("Authorization must run before opening a session.");
        }

        public Task<CatalogCommitVerification<CharacterMutationEvidence>> VerifyCommitAsync(
            CharacterMutationEvidence evidence,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Authorization must run before verification.");
    }
}
