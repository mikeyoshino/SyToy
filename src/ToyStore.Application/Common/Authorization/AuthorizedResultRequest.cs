using ToyStore.Application.Common.Messaging;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Common.Authorization;

public abstract record AuthorizedResultRequest<TResponse>
    : IResultRequest<TResponse>, IAuthorizedRequestState
{
    public abstract string RequiredPolicy { get; }

    public string? AuthorizedActorId { get; private set; }

    public abstract TResponse CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null);

    void IAuthorizedRequestState.SetAuthorizedActor(string actorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        AuthorizedActorId = actorId;
    }
}

internal interface IAuthorizedRequestState
{
    string RequiredPolicy { get; }

    string? AuthorizedActorId { get; }

    void SetAuthorizedActor(string actorId);
}
