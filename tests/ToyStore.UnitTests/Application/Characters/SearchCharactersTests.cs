using FluentValidation;
using ToyStore.Application.Characters;
using ToyStore.Application.Characters.SearchCharacters;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Models;

namespace ToyStore.UnitTests.Application.Characters;

public sealed class SearchCharactersTests
{
    private static readonly Guid UniverseId =
        Guid.Parse("91000000-0000-0000-0000-000000000001");

    [Fact]
    public void QueryDefaultsToTwentyResultsAndProductManagementPolicy()
    {
        var query = new SearchCharactersQuery(UniverseId);

        Assert.Equal(UniverseId, query.UniverseId);
        Assert.Null(query.Term);
        Assert.Equal(20, query.Limit);
        Assert.Equal(PolicyNames.CanManageProducts, query.RequiredPolicy);
    }

    [Fact]
    public async Task ValidationBehaviorReturnsStructuredThaiFailuresWithoutCallingHandler()
    {
        var query = new SearchCharactersQuery(
            Guid.Empty,
            new string('ก', 201),
            Limit: 0);
        var behavior = new ValidationBehavior<
            SearchCharactersQuery,
            Result<SearchCharactersResult>>([new SearchCharactersValidator()]);
        var handlerCalled = false;

        var result = await behavior.Handle(
            query,
            _ =>
            {
                handlerCalled = true;
                return Task.FromResult(Result<SearchCharactersResult>.Success(
                    new SearchCharactersResult([], hasExactMatch: false)));
            },
            TestContext.Current.CancellationToken);

        Assert.False(handlerCalled);
        Assert.Equal(RequestErrors.ValidationFailed, result.Error);
        Assert.Collection(
            result.ValidationFailures.OrderBy(failure => failure.PropertyName),
            failure =>
            {
                Assert.Equal(nameof(SearchCharactersQuery.Limit), failure.PropertyName);
                Assert.Equal("จำนวนผลลัพธ์ต้องอยู่ระหว่าง 1–20 รายการ", failure.ErrorMessage);
            },
            failure =>
            {
                Assert.Equal(nameof(SearchCharactersQuery.Term), failure.PropertyName);
                Assert.Equal("คำค้นหาต้องไม่เกิน 200 ตัวอักษรหลังจัดรูปแบบ", failure.ErrorMessage);
            },
            failure =>
            {
                Assert.Equal(nameof(SearchCharactersQuery.UniverseId), failure.PropertyName);
                Assert.Equal("กรุณาเลือกจักรวาล", failure.ErrorMessage);
            });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public void ValidatorAcceptsInclusiveLimitBoundaries(int limit)
    {
        var result = new SearchCharactersValidator().Validate(
            new SearchCharactersQuery(UniverseId, Limit: limit));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public void ValidatorAcceptsEveryBlankTerm(string? term)
    {
        var result = new SearchCharactersValidator().Validate(
            new SearchCharactersQuery(UniverseId, term));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void ValidatorRejectsLimitsOutsideTheAutocompleteCap(int limit)
    {
        var result = new SearchCharactersValidator().Validate(
            new SearchCharactersQuery(UniverseId, Limit: limit));

        var failure = Assert.Single(result.Errors);
        Assert.Equal(nameof(SearchCharactersQuery.Limit), failure.PropertyName);
        Assert.Equal("จำนวนผลลัพธ์ต้องอยู่ระหว่าง 1–20 รายการ", failure.ErrorMessage);
    }

    [Fact]
    public void ValidatorBoundsTheNormalizedTermRatherThanRawWhitespaceOrWidth()
    {
        var validator = new SearchCharactersValidator();
        var normalizedTwoHundred = $"  {new string('Ａ', 200)}  ";
        var shortRawTermThatNormalizesToTwoHundredOne =
            string.Concat(Enumerable.Repeat("\uFB03", 67));

        var accepted = validator.Validate(
            new SearchCharactersQuery(UniverseId, normalizedTwoHundred));
        var rejected = validator.Validate(
            new SearchCharactersQuery(UniverseId, shortRawTermThatNormalizesToTwoHundredOne));

        Assert.True(accepted.IsValid);
        var failure = Assert.Single(rejected.Errors);
        Assert.Equal(nameof(SearchCharactersQuery.Term), failure.PropertyName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public async Task HandlerMapsEveryBlankTermToNormalizedEmpty(string? term)
    {
        var option = new CharacterOption(Guid.NewGuid(), UniverseId, "Iron Man");
        var reader = new CapturingCharacterSearchReader(new CharacterSearchReadResult(
            universeAvailable: true,
            [option],
            hasExactMatch: false));
        var handler = new SearchCharactersHandler(reader);

        var result = await handler.Handle(
            new SearchCharactersQuery(UniverseId, term),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, reader.Request!.NormalizedTerm);
        Assert.Equal(20, reader.Request.Limit);
        Assert.Equal(option, Assert.Single(result.Value.Items));
        Assert.False(result.Value.HasExactMatch);
    }

    [Fact]
    public async Task HandlerNormalizesOnceAndPreservesServerExactMetadataAndCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        var option = new CharacterOption(Guid.NewGuid(), UniverseId, "Iron Man");
        var reader = new CapturingCharacterSearchReader(new CharacterSearchReadResult(
            universeAvailable: true,
            [option],
            hasExactMatch: true));
        var handler = new SearchCharactersHandler(reader);

        var result = await handler.Handle(
            new SearchCharactersQuery(UniverseId, "  Ｉron\u2003  Man  ", Limit: 7),
            cancellationSource.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal("IRON MAN", reader.Request!.NormalizedTerm);
        Assert.Equal(7, reader.Request.Limit);
        Assert.Equal(cancellationSource.Token, reader.CancellationToken);
        Assert.True(result.Value.HasExactMatch);
        Assert.Equal(option, Assert.Single(result.Value.Items));
    }

    [Fact]
    public async Task HandlerMapsUnavailableUniverseToTypedSafeFailure()
    {
        var reader = new CapturingCharacterSearchReader(new CharacterSearchReadResult(
            universeAvailable: false,
            [],
            hasExactMatch: false));
        var handler = new SearchCharactersHandler(reader);

        var result = await handler.Handle(
            new SearchCharactersQuery(UniverseId, "Batman"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(CharacterErrors.UniverseUnavailable, result.Error);
        Assert.Empty(result.ValidationFailures);
    }

    [Fact]
    public async Task HandlerPropagatesReaderCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var handler = new SearchCharactersHandler(new CancellingCharacterSearchReader());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.Handle(
            new SearchCharactersQuery(UniverseId, "Batman"),
            cancellationSource.Token));
    }

    [Theory]
    [InlineData(false, false, "Authorization.Unauthorized")]
    [InlineData(true, false, "Authorization.Forbidden")]
    public async Task AuthorizationShortCircuitsActualQueryBeforeValidationAndReader(
        bool isAuthenticated,
        bool isAuthorized,
        string expectedErrorCode)
    {
        var query = new SearchCharactersQuery(
            Guid.Empty,
            new string('ก', 201),
            Limit: 0);
        var reader = new CountingCharacterSearchReader();
        var validator = new CountingSearchCharactersValidator();
        var validation = new ValidationBehavior<
            SearchCharactersQuery,
            Result<SearchCharactersResult>>([validator]);
        var authorization = new AuthorizationBehavior<
            SearchCharactersQuery,
            Result<SearchCharactersResult>>(new StubAuthorization(
                new CurrentUserAuthorizationResult(
                    isAuthenticated,
                    isAuthorized,
                    isAuthenticated ? "actor-1" : null)));
        var handler = new SearchCharactersHandler(reader);

        var result = await authorization.Handle(
            query,
            cancellationToken => validation.Handle(
                query,
                handlerCancellationToken => handler.Handle(query, handlerCancellationToken),
                cancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedErrorCode, result.Error.Code);
        Assert.Equal(0, validator.CallCount);
        Assert.Equal(0, reader.CallCount);
        Assert.Null(query.AuthorizedActorId);
    }

    private sealed class CapturingCharacterSearchReader(CharacterSearchReadResult response)
        : ICharacterSearchReader
    {
        public CharacterSearchReadRequest? Request { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<CharacterSearchReadResult> ReadAsync(
            CharacterSearchReadRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            CancellationToken = cancellationToken;
            return Task.FromResult(response);
        }
    }

    private sealed class CancellingCharacterSearchReader : ICharacterSearchReader
    {
        public Task<CharacterSearchReadResult> ReadAsync(
            CharacterSearchReadRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("A cancelled read must not continue.");
        }
    }

    private sealed class CountingCharacterSearchReader : ICharacterSearchReader
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

    private sealed class CountingSearchCharactersValidator
        : FluentValidation.AbstractValidator<SearchCharactersQuery>
    {
        public CountingSearchCharactersValidator()
        {
            RuleFor(query => query).Custom((_, _) => CallCount++);
        }

        public int CallCount { get; private set; }
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
}
