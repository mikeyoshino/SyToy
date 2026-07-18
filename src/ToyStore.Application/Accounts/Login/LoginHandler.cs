using MediatR;
using ToyStore.Application.Common.Interfaces;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Accounts.Login;

public sealed class LoginHandler(IIdentityService identityService)
    : IRequestHandler<LoginCommand, Result<LoginResult>>
{
    public Task<Result<LoginResult>> Handle(
        LoginCommand request,
        CancellationToken cancellationToken) =>
        identityService.PasswordSignInAsync(
            request.Email,
            request.Password,
            request.RememberMe,
            cancellationToken);
}
