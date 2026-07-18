using MediatR;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Messaging;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Common.Behaviors;

public sealed class AuthorizationBehavior<TRequest, TResponse>(
    ICurrentUserAuthorization currentUserAuthorization)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        if (request is not IAuthorizedRequestState authorizedRequest)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        if (request is not IResultRequest<TResponse> resultRequest)
        {
            throw new InvalidOperationException(
                $"Authorized request {typeof(TRequest).Name} must implement IResultRequest.");
        }

        var authorization = await currentUserAuthorization.AuthorizeAsync(
            authorizedRequest.RequiredPolicy,
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAuthenticated)
        {
            return resultRequest.CreateFailure(RequestErrors.Unauthorized);
        }

        if (!authorization.IsAuthorized || string.IsNullOrWhiteSpace(authorization.ActorId))
        {
            return resultRequest.CreateFailure(RequestErrors.Forbidden);
        }

        authorizedRequest.SetAuthorizedActor(authorization.ActorId);
        return await next(cancellationToken).ConfigureAwait(false);
    }
}
