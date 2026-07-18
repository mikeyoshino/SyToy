using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Carts;

namespace ToyStore.Application.Cart.ClearCart;

public sealed class ClearCartHandler(ICartMutationSessionFactory sessionFactory, TimeProvider timeProvider)
    : IRequestHandler<ClearCartCommand, Result<CartMutationResult>>
{
    public async Task<Result<CartMutationResult>> Handle(ClearCartCommand request, CancellationToken cancellationToken)
    {
        var customerId = request.AuthorizedActorId
            ?? throw new InvalidOperationException("Cart command reached its handler without an authorized customer.");
        await using var session = await sessionFactory.OpenAsync(cancellationToken);
        return await session.ExecuteOnceAsync(async token =>
        {
            var locked = await session.LockCustomerCartAsync(customerId, token);
            if (!locked.CustomerExists) return Result<CartMutationResult>.Failure(CartErrors.CustomerUnavailable);
            var fingerprint = CartMutationSupport.Fingerprint(CartOperationType.Clear, request.ExpectedVersion);
            var retry = CartMutationSupport.ResolveRetry(await session.FindOperationAsync(request.OperationId, token),
                locked.Cart, CartOperationType.Clear, fingerprint);
            if (retry is not null) return retry;
            if (locked.Cart is null) return Result<CartMutationResult>.Failure(CartErrors.CartNotFound);
            if (locked.Cart.Version != request.ExpectedVersion) return Result<CartMutationResult>.Failure(CartErrors.StaleVersion);
            var now = timeProvider.GetUtcNow();
            locked.Cart.Clear(locked.Cart.Version, now);
            session.Add(CartMutationSupport.ToOperation(request.OperationId, locked.Cart,
                CartOperationType.Clear, fingerprint, now));
            return Result<CartMutationResult>.Success(CartMutationSupport.ToResult(locked.Cart));
        }, cancellationToken);
    }
}
