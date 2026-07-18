using System.Text.Json;
using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Carts;
using ToyStore.Domain.Products;

namespace ToyStore.Application.Cart.MergeAnonymousCart;

public sealed class MergeAnonymousCartHandler(
    ICartMutationSessionFactory sessionFactory,
    TimeProvider timeProvider)
    : IRequestHandler<MergeAnonymousCartCommand, Result<MergeAnonymousCartResult>>
{
    public async Task<Result<MergeAnonymousCartResult>> Handle(
        MergeAnonymousCartCommand request,
        CancellationToken cancellationToken)
    {
        var customerId = request.AuthorizedActorId
            ?? throw new InvalidOperationException("Cart command reached its handler without an authorized customer.");
        await using var session = await sessionFactory.OpenAsync(cancellationToken);
        return await session.ExecuteOnceAsync(async token =>
        {
            var locked = await session.LockCustomerCartAsync(customerId, token);
            if (!locked.CustomerExists)
                return Result<MergeAnonymousCartResult>.Failure(CartErrors.CustomerUnavailable);

            var grouped = request.Items
                .GroupBy(item => item.ProductId)
                .Select(group => new GroupedItem(group.Key, group.Sum(item => (long)item.Quantity)))
                .OrderBy(item => item.ProductId)
                .ToArray();
            var fingerprint = CartMutationSupport.Fingerprint(
                CartOperationType.Merge,
                string.Join(';', grouped.Select(item => $"{item.ProductId:N}:{item.Quantity}")));
            var previous = await session.FindOperationAsync(request.OperationId, token);
            if (previous is not null)
            {
                var retry = CartMutationSupport.ResolveRetry(
                    previous, locked.Cart, CartOperationType.Merge, fingerprint);
                if (retry is null) throw new InvalidOperationException("Retry resolution unexpectedly returned no result.");
                if (retry.IsFailure)
                    return Result<MergeAnonymousCartResult>.Failure(retry.Error);
                var replay = JsonSerializer.Deserialize<MergeReplayData>(previous.ResultData!)
                    ?? throw new InvalidOperationException("Stored cart merge result could not be replayed.");
                return Result<MergeAnonymousCartResult>.Success(new(
                    retry.Value, replay.RejectedItems, replay.ClampedItems));
            }

            var now = timeProvider.GetUtcNow();
            var cart = locked.Cart ?? ToyStore.Domain.Carts.Cart.Create(Guid.NewGuid(), customerId, now);
            if (locked.Cart is null) session.Add(cart);
            var products = await session.FindProductsAsync(
                grouped.Select(item => item.ProductId).ToArray(), token);
            var rejected = new List<MergeRejectedItem>();
            var clamped = new List<MergeClampedItem>();

            foreach (var input in grouped)
            {
                if (!products.TryGetValue(input.ProductId, out var product)
                    || product.Status != ProductStatus.Published
                    || product.SaleType != SaleType.InStock)
                {
                    rejected.Add(new(input.ProductId, CartErrors.ProductUnavailable.Message));
                    continue;
                }

                var existing = cart.Items.SingleOrDefault(item => item.ProductId == input.ProductId);
                var requested = (long)(existing?.Quantity ?? 0) + input.Quantity;
                var applied = (int)Math.Min(requested, CartLimits.MaximumQuantityPerItem);
                if (requested > applied)
                {
                    clamped.Add(new(input.ProductId,
                        requested > int.MaxValue ? int.MaxValue : (int)requested, applied));
                }

                if (existing is null)
                    cart.Add(product, applied, cart.Version, now);
                else if (existing.Quantity != applied)
                    cart.SetQuantity(product.Id, applied, cart.Version, now);
            }

            var resultData = JsonSerializer.Serialize(new MergeReplayData(rejected, clamped));
            session.Add(CartMutationSupport.ToOperation(request.OperationId, cart,
                CartOperationType.Merge, fingerprint, now, resultData));
            return Result<MergeAnonymousCartResult>.Success(new(
                CartMutationSupport.ToResult(cart), rejected, clamped));
        }, cancellationToken);
    }

    private sealed record GroupedItem(Guid ProductId, long Quantity);

    private sealed record MergeReplayData(
        IReadOnlyList<MergeRejectedItem> RejectedItems,
        IReadOnlyList<MergeClampedItem> ClampedItems);
}
