using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Carts;
using ToyStore.Domain.Products;

namespace ToyStore.Application.Cart.ChangeCartItemQuantity;

public sealed class ChangeCartItemQuantityHandler(
    ICartMutationSessionFactory sessionFactory,
    TimeProvider timeProvider)
    : IRequestHandler<ChangeCartItemQuantityCommand, Result<CartMutationResult>>
{
    public async Task<Result<CartMutationResult>> Handle(
        ChangeCartItemQuantityCommand request,
        CancellationToken cancellationToken)
    {
        var customerId = request.AuthorizedActorId
            ?? throw new InvalidOperationException("Cart command reached its handler without an authorized customer.");
        await using var session = await sessionFactory.OpenAsync(cancellationToken);
        return await session.ExecuteOnceAsync(async token =>
        {
            var locked = await session.LockCustomerCartAsync(customerId, token);
            if (!locked.CustomerExists) return Result<CartMutationResult>.Failure(CartErrors.CustomerUnavailable);
            var fingerprint = CartMutationSupport.Fingerprint(
                CartOperationType.ChangeQuantity, request.ProductId, request.Quantity, request.ExpectedVersion);
            var retry = CartMutationSupport.ResolveRetry(
                await session.FindOperationAsync(request.OperationId, token), locked.Cart,
                CartOperationType.ChangeQuantity, fingerprint);
            if (retry is not null) return retry;
            if (locked.Cart is null) return Result<CartMutationResult>.Failure(CartErrors.CartNotFound);
            if (locked.Cart.Version != request.ExpectedVersion) return Result<CartMutationResult>.Failure(CartErrors.StaleVersion);
            var product = await session.FindProductAsync(request.ProductId, token);
            if (product is null || product.Status != ProductStatus.Published || product.SaleType != SaleType.InStock)
                return Result<CartMutationResult>.Failure(CartErrors.ProductUnavailable);
            var now = timeProvider.GetUtcNow();
            try { locked.Cart.SetQuantity(request.ProductId, request.Quantity, locked.Cart.Version, now); }
            catch (CartRuleException exception) { return Result<CartMutationResult>.Failure(CartMutationSupport.Map(exception)); }
            session.Add(CartMutationSupport.ToOperation(request.OperationId, locked.Cart,
                CartOperationType.ChangeQuantity, fingerprint, now));
            return Result<CartMutationResult>.Success(CartMutationSupport.ToResult(locked.Cart));
        }, cancellationToken);
    }
}
