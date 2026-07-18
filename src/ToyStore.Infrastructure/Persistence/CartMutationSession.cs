using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ToyStore.Application.Cart;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Carts;
using ToyStore.Domain.Products;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class CartMutationSessionFactory(IDbContextFactory<ApplicationDbContext> contextFactory)
    : ICartMutationSessionFactory
{
    public async ValueTask<ICartMutationSession> OpenAsync(CancellationToken cancellationToken) =>
        new CartMutationSession(await contextFactory.CreateDbContextAsync(cancellationToken));
}

internal sealed class CartMutationSession(ApplicationDbContext db) : ICartMutationSession
{
    private IDbContextTransaction? transaction;
    private bool active;
    private bool customerLocked;
    private int executed;

    public async Task<LockedCustomerCart> LockCustomerCartAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        EnsureActive();
        if (customerLocked) throw new InvalidOperationException("Customer cart can be locked only once per session.");
        customerLocked = true;
        var users = await db.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {customerId} FOR UPDATE")
            .AsNoTracking().ToArrayAsync(cancellationToken);
        if (users.Length == 0) return new(false, null);
        var cart = await db.Carts.Include(current => current.Items)
            .SingleOrDefaultAsync(current => current.CustomerId == customerId, cancellationToken);
        return new(true, cart);
    }

    public Task<Product?> FindProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        EnsureCustomerLocked();
        return db.Products.SingleOrDefaultAsync(product => product.Id == productId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, Product>> FindProductsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        EnsureCustomerLocked();
        var ids = productIds.Distinct().ToArray();
        return await db.Products.Where(product => ids.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken);
    }

    public Task<CartOperation?> FindOperationAsync(Guid operationId, CancellationToken cancellationToken)
    {
        EnsureCustomerLocked();
        return db.CartOperations.AsNoTracking()
            .SingleOrDefaultAsync(operation => operation.Id == operationId, cancellationToken);
    }

    public void Add(Cart cart)
    {
        EnsureCustomerLocked();
        db.Carts.Add(cart);
    }

    public void Add(CartOperation operation)
    {
        EnsureCustomerLocked();
        db.CartOperations.Add(operation);
    }

    public async Task<Result<T>> ExecuteOnceAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (Interlocked.Exchange(ref executed, 1) != 0)
            throw new InvalidOperationException("Cart mutation session can execute only once.");
        transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        active = true;
        try
        {
            var result = await operation(cancellationToken);
            if (result.IsFailure)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return result;
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            active = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (transaction is not null) await transaction.DisposeAsync();
        await db.DisposeAsync();
    }

    private void EnsureActive()
    {
        if (!active) throw new InvalidOperationException("Cart mutation session has no active operation.");
    }

    private void EnsureCustomerLocked()
    {
        EnsureActive();
        if (!customerLocked) throw new InvalidOperationException("Customer must be locked first.");
    }
}
