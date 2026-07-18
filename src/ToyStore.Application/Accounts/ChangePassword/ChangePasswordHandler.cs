using MediatR;
using ToyStore.Application.Common.Interfaces;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Accounts.ChangePassword;

public sealed class ChangePasswordHandler(
    IIdentityService identityService,
    IUserContext userContext) : IRequestHandler<ChangePasswordCommand, Result>
{
    public Task<Result> Handle(
        ChangePasswordCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userContext.UserId))
        {
            return Task.FromResult(Result.Failure(AccountErrors.NotAuthenticated));
        }

        return identityService.ChangePasswordAsync(
            userContext.UserId,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);
    }
}
