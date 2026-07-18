using ToyStore.Application.Accounts.Login;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<Result<string>> RegisterCustomerAsync(
        string email,
        string password,
        CancellationToken cancellationToken);

    Task<Result<LoginResult>> PasswordSignInAsync(
        string email,
        string password,
        bool rememberMe,
        CancellationToken cancellationToken);

    Task<Result> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken);

    Task SignOutAsync(CancellationToken cancellationToken);
}
