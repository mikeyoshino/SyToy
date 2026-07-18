namespace ToyStore.Application.Common.Authorization;

public sealed record CurrentUserAuthorizationResult(
    bool IsAuthenticated,
    bool IsAuthorized,
    string? ActorId);
