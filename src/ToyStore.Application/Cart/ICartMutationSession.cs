using ToyStore.Application.Common.Models;
using ToyStore.Domain.Carts;
using ToyStore.Domain.Products;

namespace ToyStore.Application.Cart;

public interface ICartMutationSessionFactory
{
    ValueTask<ICartMutationSession> OpenAsync(CancellationToken cancellationToken);
}

public interface ICartMutationSession : IAsyncDisposable
{
    Task<LockedCustomerCart> LockCustomerCartAsync(
        string customerId,
        CancellationToken cancellationToken);

    Task<Product?> FindProductAsync(Guid productId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, Product>> FindProductsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken);

    Task<CartOperation?> FindOperationAsync(
        Guid operationId,
        CancellationToken cancellationToken);

    void Add(ToyStore.Domain.Carts.Cart cart);

    void Add(CartOperation operation);

    Task<Result<T>> ExecuteOnceAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken);
}

public sealed record LockedCustomerCart(
    bool CustomerExists,
    ToyStore.Domain.Carts.Cart? Cart);
