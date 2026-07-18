using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ToyStore.Application.Accounts;
using ToyStore.Application.Accounts.Login;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Interfaces;
using ToyStore.Application.Common.Models;
using ToyStore.Infrastructure.Persistence;

namespace ToyStore.Infrastructure.Identity;

public sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext dbContext) : IIdentityService
{
    public async Task<Result<string>> RegisterCustomerAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return Result<string>.Failure(AccountErrors.EmailAlreadyUsed);
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var user = new ApplicationUser
            {
                Email = email,
                UserName = email,
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return MapRegistrationFailure(createResult);
            }

            var roleResult = await userManager.AddToRoleAsync(user, RoleNames.Customer);
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<string>.Failure(AccountErrors.RegistrationFailed);
            }

            await transaction.CommitAsync(cancellationToken);
            return Result<string>.Success(user.Id);
        });
    }

    public async Task<Result<LoginResult>> PasswordSignInAsync(
        string email,
        string password,
        bool rememberMe,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var signInResult = await signInManager.PasswordSignInAsync(
            email,
            password,
            rememberMe,
            lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
        {
            return Result<LoginResult>.Failure(AccountErrors.LockedOut);
        }

        if (!signInResult.Succeeded)
        {
            return Result<LoginResult>.Failure(AccountErrors.InvalidCredentials);
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return Result<LoginResult>.Failure(AccountErrors.InvalidCredentials);
        }

        return Result<LoginResult>.Success(new LoginResult(user.MustChangePassword));
    }

    public async Task<Result> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Result.Failure(AccountErrors.NotAuthenticated);
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var passwordResult =
                await userManager.ChangePasswordAsync(user, currentPassword, newPassword);

            if (!passwordResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result.Failure(AccountErrors.PasswordChangeFailed);
            }

            user.MustChangePassword = false;
            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result.Failure(AccountErrors.PasswordChangeFailed);
            }

            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        });

        if (result.IsSuccess)
        {
            await signInManager.RefreshSignInAsync(user);
        }

        return result;
    }

    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await signInManager.SignOutAsync();
    }

    private static Result<string> MapRegistrationFailure(IdentityResult identityResult)
    {
        if (identityResult.Errors.Any(error =>
                string.Equals(error.Code, nameof(IdentityErrorDescriber.DuplicateEmail), StringComparison.Ordinal)
                || string.Equals(error.Code, nameof(IdentityErrorDescriber.DuplicateUserName), StringComparison.Ordinal)))
        {
            return Result<string>.Failure(AccountErrors.EmailAlreadyUsed);
        }

        return Result<string>.Failure(AccountErrors.RegistrationFailed);
    }
}
