using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Carts;

namespace ToyStore.Application.Cart;

internal static class CartMutationSupport
{
    public static string Fingerprint(
        CartOperationType type,
        params object?[] values)
    {
        var canonical = string.Join('|', new[] { type.ToString() }.Concat(values.Select(Format)));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static Result<CartMutationResult>? ResolveRetry(
        CartOperation? operation,
        ToyStore.Domain.Carts.Cart? cart,
        CartOperationType expectedType,
        string expectedFingerprint)
    {
        if (operation is null)
        {
            return null;
        }

        if (cart is null
            || operation.CartId != cart.Id
            || operation.Type != expectedType
            || !string.Equals(
                operation.IntentFingerprint,
                expectedFingerprint,
                StringComparison.Ordinal))
        {
            return Result<CartMutationResult>.Failure(CartErrors.OperationConflict);
        }

        return Result<CartMutationResult>.Success(new(
            operation.CartId,
            operation.ResultingCartVersion,
            operation.ResultingTotalQuantity,
            WasIdempotentRetry: true));
    }

    public static CartOperation ToOperation(
        Guid operationId,
        ToyStore.Domain.Carts.Cart cart,
        CartOperationType type,
        string fingerprint,
        DateTimeOffset occurredAtUtc,
        string? resultData = null) =>
        CartOperation.Create(
            operationId,
            cart.Id,
            type,
            fingerprint,
            cart.Version,
            TotalQuantity(cart),
            resultData,
            occurredAtUtc);

    public static CartMutationResult ToResult(
        ToyStore.Domain.Carts.Cart cart,
        bool wasRetry = false) =>
        new(
            cart.Id,
            cart.Version,
            TotalQuantity(cart),
            wasRetry);

    public static Error Map(CartRuleException exception) => exception.Rule switch
    {
        CartRule.ProductMustBePublishedInStock => CartErrors.ProductUnavailable,
        CartRule.QuantityExceedsLimit => CartErrors.QuantityExceedsLimit,
        CartRule.CartItemNotFound => CartErrors.ItemNotFound,
        CartRule.CartOwnershipMismatch => CartErrors.OwnershipMismatch,
        CartRule.ConcurrencyVersionMismatch => CartErrors.StaleVersion,
        _ => throw exception,
    };

    private static string Format(object? value) => value switch
    {
        null => "null",
        Guid guid => guid.ToString("N"),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static long TotalQuantity(ToyStore.Domain.Carts.Cart cart) =>
        cart.Items.Sum(item => (long)item.Quantity);
}
