namespace ToyStore.Domain.Carts;

public sealed record AnonymousCartItem
{
    private AnonymousCartItem(Guid productId, int quantity)
    {
        ProductId = productId;
        Quantity = quantity;
    }

    public Guid ProductId { get; }

    public int Quantity { get; }

    public static AnonymousCartItem Create(Guid productId, int quantity)
    {
        CartRules.EnsureProductIdentity(productId);
        CartRules.EnsureQuantity(quantity);
        return new AnonymousCartItem(productId, quantity);
    }
}
