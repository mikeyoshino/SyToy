using MediatR;
using ToyStore.Application.Common.Interfaces;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Accounts.Logout;

public sealed class LogoutHandler(IIdentityService identityService)
    : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(
        LogoutCommand request,
        CancellationToken cancellationToken)
    {
        await identityService.SignOutAsync(cancellationToken);
        return Result.Success();
    }
}
