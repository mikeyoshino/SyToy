using FluentValidation;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Models;

namespace ToyStore.UnitTests.Application;

public sealed class AuthorizationBehaviorTests
{
    private static readonly string[] ExpectedActorIds = ["actor-1", "actor-2"];

    [Fact]
    public async Task AnonymousRequestShortCircuitsBeforeValidationAndHandler()
    {
        var authorizer = new StubCurrentUserAuthorization(
            new CurrentUserAuthorizationResult(
                IsAuthenticated: false,
                IsAuthorized: false,
                ActorId: null));
        var request = new TestAuthorizedRequest(string.Empty);
        var validator = new CountingValidator();
        var validation = new ValidationBehavior<TestAuthorizedRequest, Result<string>>([validator]);
        var authorization = new AuthorizationBehavior<TestAuthorizedRequest, Result<string>>(authorizer);
        var handlerCalled = false;

        var result = await authorization.Handle(
            request,
            cancellationToken => validation.Handle(
                request,
                _ =>
                {
                    handlerCalled = true;
                    return Task.FromResult(Result<string>.Success("handled"));
                },
                cancellationToken),
            CancellationToken.None);

        Assert.Equal("Authorization.Unauthorized", result.Error.Code);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Empty(result.ValidationFailures);
        Assert.Equal(0, validator.CallCount);
        Assert.False(handlerCalled);
        Assert.Null(request.AuthorizedActorId);
        Assert.Equal(PolicyNames.CanManageProducts, authorizer.ReceivedPolicy);
    }

    [Fact]
    public async Task AuthenticatedUserWithoutPolicyReturnsForbidden()
    {
        var authorizer = new StubCurrentUserAuthorization(
            new CurrentUserAuthorizationResult(
                IsAuthenticated: true,
                IsAuthorized: false,
                ActorId: "admin-1"));
        var request = new TestAuthorizedRequest("Molly");
        var behavior = new AuthorizationBehavior<TestAuthorizedRequest, Result<string>>(authorizer);

        var result = await behavior.Handle(
            request,
            _ => Task.FromResult(Result<string>.Success("handled")),
            CancellationToken.None);

        Assert.Equal("Authorization.Forbidden", result.Error.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Null(request.AuthorizedActorId);
    }

    [Fact]
    public async Task AuthorizedOverlappingRequestsKeepPrincipalActorsOnTheirOwnInstances()
    {
        var authorizer = new OverlappingCurrentUserAuthorization();
        var first = new TestAuthorizedRequest("first");
        var second = new TestAuthorizedRequest("second");
        var behavior = new AuthorizationBehavior<TestAuthorizedRequest, Result<string>>(authorizer);

        Task<Result<string>> ExecuteAsync(TestAuthorizedRequest request) => behavior.Handle(
            request,
            _ => Task.FromResult(Result<string>.Success(request.AuthorizedActorId!)),
            CancellationToken.None);

        var results = await Task.WhenAll(ExecuteAsync(first), ExecuteAsync(second));

        Assert.Equal(2, results.Select(result => result.Value).Distinct().Count());
        Assert.Contains(first.AuthorizedActorId, ExpectedActorIds);
        Assert.Contains(second.AuthorizedActorId, ExpectedActorIds);
        Assert.NotEqual(first.AuthorizedActorId, second.AuthorizedActorId);

        var actorProperty = typeof(AuthorizedResultRequest<Result<string>>)
            .GetProperty(nameof(AuthorizedResultRequest<Result<string>>.AuthorizedActorId));
        Assert.NotNull(actorProperty);
        Assert.False(actorProperty.SetMethod?.IsPublic ?? false);
    }

    private sealed record TestAuthorizedRequest(string Name)
        : AuthorizedResultRequest<Result<string>>
    {
        public override string RequiredPolicy => PolicyNames.CanManageProducts;

        public override Result<string> CreateFailure(
            Error requestError,
            IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
            Result<string>.Failure(requestError, validationFailures);
    }

    private sealed class CountingValidator : AbstractValidator<TestAuthorizedRequest>
    {
        public CountingValidator()
        {
            RuleFor(request => request.Name).Custom((_, context) =>
            {
                CallCount++;
                context.AddFailure(nameof(TestAuthorizedRequest.Name), "กรุณากรอกชื่อ");
            });
        }

        public int CallCount { get; private set; }
    }

    private sealed class StubCurrentUserAuthorization(CurrentUserAuthorizationResult result)
        : ICurrentUserAuthorization
    {
        public string? ReceivedPolicy { get; private set; }

        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policyName,
            CancellationToken cancellationToken)
        {
            ReceivedPolicy = policyName;
            return Task.FromResult(result);
        }
    }

    private sealed class OverlappingCurrentUserAuthorization : ICurrentUserAuthorization
    {
        private readonly TaskCompletionSource barrier = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int actorSequence;
        private int entered;

        public async Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policyName,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref entered) == 2)
            {
                barrier.TrySetResult();
            }

            await barrier.Task.WaitAsync(cancellationToken);
            var actorId = $"actor-{Interlocked.Increment(ref actorSequence)}";
            return new CurrentUserAuthorizationResult(
                IsAuthenticated: true,
                IsAuthorized: true,
                actorId);
        }
    }
}
