namespace ToyStore.Domain.Carts;

public sealed class CartItem
{
    private CartItem()
    {
    }

    internal CartItem(Guid productId, int quantity)
    {
        ProductId = productId;
        Quantity = quantity;
    }

    public Guid ProductId { get; private set; }

    public int Quantity { get; private set; }

    internal void SetQuantity(int quantity) => Quantity = quantity;
}
