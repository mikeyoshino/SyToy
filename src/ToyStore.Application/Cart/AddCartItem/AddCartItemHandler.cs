using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Carts;
using ToyStore.Domain.Products;

namespace ToyStore.Application.Cart.AddCartItem;

public sealed class AddCartItemHandler(
    ICartMutationSessionFactory sessionFactory,
    TimeProvider timeProvider)
    : IRequestHandler<AddCartItemCommand, Result<CartMutationResult>>
{
    public async Task<Result<CartMutationResult>> Handle(
        AddCartItemCommand request,
        CancellationToken cancellationToken)
    {
        var customerId = request.AuthorizedActorId
            ?? throw new InvalidOperationException("Cart command reached its handler without an authorized customer.");
        await using var session = await sessionFactory.OpenAsync(cancellationToken);
        return await session.ExecuteOnceAsync(async operationToken =>
        {
            var locked = await session.LockCustomerCartAsync(customerId, operationToken);
            if (!locked.CustomerExists)
            {
                return Result<CartMutationResult>.Failure(CartErrors.CustomerUnavailable);
            }

            var fingerprint = CartMutationSupport.Fingerprint(
                CartOperationType.Add,
                request.ProductId,
                request.Quantity,
                request.ExpectedVersion);
            var previousOperation = await session.FindOperationAsync(
                request.OperationId,
                operationToken);
            var retry = CartMutationSupport.ResolveRetry(
                previousOperation,
                locked.Cart,
                CartOperationType.Add,
                fingerprint);
            if (retry is not null)
            {
                return retry;
            }

            var product = await session.FindProductAsync(request.ProductId, operationToken);
            if (product is null
                || product.Status != ProductStatus.Published
                || product.SaleType != SaleType.InStock)
            {
                return Result<CartMutationResult>.Failure(CartErrors.ProductUnavailable);
            }

            var now = timeProvider.GetUtcNow();
            var cart = locked.Cart;
            if (cart is null)
            {
                if (request.ExpectedVersion != 0)
                {
                    return Result<CartMutationResult>.Failure(CartErrors.StaleVersion);
                }

                cart = ToyStore.Domain.Carts.Cart.Create(Guid.NewGuid(), customerId, now);
                session.Add(cart);
            }
            else if (request.ExpectedVersion != cart.Version)
            {
                return Result<CartMutationResult>.Failure(CartErrors.StaleVersion);
            }

            try
            {
                cart.Add(product, request.Quantity, cart.Version, now);
            }
            catch (CartRuleException exception)
            {
                return Result<CartMutationResult>.Failure(CartMutationSupport.Map(exception));
            }

            session.Add(CartMutationSupport.ToOperation(
                request.OperationId,
                cart,
                CartOperationType.Add,
                fingerprint,
                now));
            return Result<CartMutationResult>.Success(CartMutationSupport.ToResult(cart));
        }, cancellationToken);
    }
}
