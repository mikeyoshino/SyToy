using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ToyStore.Application.Accounts;
using ToyStore.Application.Accounts.BootstrapAdmin;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Interfaces;
using ToyStore.Application.Common.Models;
using ToyStore.Infrastructure.Persistence;

namespace ToyStore.Infrastructure.Identity;

public sealed class AdminBootstrapper(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext) : IAdminBootstrapper
{
    private const long FirstAdminAdvisoryLockKey = 8_240_260_717_001;

    public async Task<Result<BootstrapAdminResult>> CreateFirstAdminAsync(
        string email,
        string temporaryPassword,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({FirstAdminAdvisoryLockKey})",
                cancellationToken);

            if ((await userManager.GetUsersInRoleAsync(RoleNames.Admin)).Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<BootstrapAdminResult>.Failure(AccountErrors.AdminAlreadyExists);
            }

            if (await userManager.FindByEmailAsync(email) is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<BootstrapAdminResult>.Failure(AccountErrors.AdminBootstrapFailed);
            }

            var user = new ApplicationUser
            {
                Email = email,
                UserName = email,
                MustChangePassword = true,
            };

            var createResult = await userManager.CreateAsync(user, temporaryPassword);
            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<BootstrapAdminResult>.Failure(AccountErrors.AdminBootstrapFailed);
            }

            var roleResult = await userManager.AddToRoleAsync(user, RoleNames.Admin);
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<BootstrapAdminResult>.Failure(AccountErrors.AdminBootstrapFailed);
            }

            await transaction.CommitAsync(cancellationToken);
            return Result<BootstrapAdminResult>.Success(new BootstrapAdminResult(user.Id));
        });
    }
}
