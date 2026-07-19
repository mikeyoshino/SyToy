using Microsoft.EntityFrameworkCore;
using ToyStore.Application.Addresses.SavedAddresses;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Addresses;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class SavedAddressStore(IDbContextFactory<ApplicationDbContext> contextFactory)
    : ISavedAddressStore
{
    public async Task<IReadOnlyList<SavedAddressView>> ListAsync(
        string customerId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var addresses = await db.SavedAddresses.AsNoTracking()
            .Where(address => address.CustomerId == customerId)
            .OrderByDescending(address => address.IsDefault)
            .ThenByDescending(address => address.UpdatedAtUtc)
            .ToArrayAsync(cancellationToken);
        return addresses.Select(ToView).ToArray();
    }

    public async Task<Result<SavedAddressView>> CreateAsync(
        SavedAddress address, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockCustomerAsync(db, address.CustomerId, cancellationToken);

        var existing = await db.SavedAddresses
            .Where(saved => saved.CustomerId == address.CustomerId)
            .OrderByDescending(saved => saved.UpdatedAtUtc)
            .ToArrayAsync(cancellationToken);
        if (existing.Length >= 5)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<SavedAddressView>.Failure(SavedAddressErrors.LimitReached);
        }

        if (existing.Length == 0 && !address.IsDefault)
            address.MakeDefault(address.CreatedAtUtc);
        if (address.IsDefault)
        {
            foreach (var current in existing.Where(current => current.IsDefault))
                current.ClearDefault(address.CreatedAtUtc);
            await db.SaveChangesAsync(cancellationToken);
        }

        db.SavedAddresses.Add(address);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<SavedAddressView>.Success(ToView(address));
    }

    public async Task<Result> DeleteAsync(string customerId, Guid addressId,
        DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockCustomerAsync(db, customerId, cancellationToken);
        var target = await db.SavedAddresses.SingleOrDefaultAsync(
            address => address.CustomerId == customerId && address.Id == addressId,
            cancellationToken);
        if (target is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(SavedAddressErrors.NotFound);
        }

        var wasDefault = target.IsDefault;
        db.SavedAddresses.Remove(target);
        await db.SaveChangesAsync(cancellationToken);
        if (wasDefault)
        {
            var replacement = await db.SavedAddresses
                .Where(address => address.CustomerId == customerId)
                .OrderByDescending(address => address.UpdatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            if (replacement is not null)
            {
                replacement.MakeDefault(nowUtc);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> SetDefaultAsync(string customerId, Guid addressId,
        DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockCustomerAsync(db, customerId, cancellationToken);
        var addresses = await db.SavedAddresses
            .Where(address => address.CustomerId == customerId)
            .ToArrayAsync(cancellationToken);
        var target = addresses.SingleOrDefault(address => address.Id == addressId);
        if (target is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(SavedAddressErrors.NotFound);
        }
        if (target.IsDefault)
        {
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }

        foreach (var current in addresses.Where(address => address.IsDefault))
            current.ClearDefault(nowUtc);
        await db.SaveChangesAsync(cancellationToken);
        target.MakeDefault(nowUtc);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private static Task<int> LockCustomerAsync(ApplicationDbContext db, string customerId,
        CancellationToken cancellationToken) => db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"AspNetUsers\" WHERE \"Id\" = {customerId} FOR UPDATE",
            cancellationToken);

    private static SavedAddressView ToView(SavedAddress saved) => new(saved.Id, saved.Label,
        saved.Address.RecipientName, saved.Address.PhoneNumber, saved.Address.AddressLine,
        saved.ProvinceId, saved.DistrictId, saved.SubDistrictId, saved.Address.Province,
        saved.Address.District, saved.Address.SubDistrict, saved.Address.PostalCode,
        saved.IsDefault);
}
