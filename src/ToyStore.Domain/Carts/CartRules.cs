namespace ToyStore.Domain.Carts;

internal static class CartRules
{
    public static void EnsureProductIdentity(Guid productId)
    {
        if (productId == Guid.Empty)
        {
            throw new CartRuleException(CartRule.ProductIdentityRequired);
        }
    }

    public static void EnsureQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new CartRuleException(CartRule.QuantityMustBePositive);
        }

        if (quantity > CartLimits.MaximumQuantityPerItem)
        {
            throw new CartRuleException(CartRule.QuantityExceedsLimit);
        }
    }

    public static void EnsureUtc(DateTimeOffset instant)
    {
        if (instant.Offset != TimeSpan.Zero)
        {
            throw new CartRuleException(CartRule.AuditInstantMustBeUtc);
        }
    }
}
