namespace ToyStore.Application.Cart;

public interface ICartReader
{
    Task<CustomerCartView> GetAsync(string customerId, CancellationToken cancellationToken);

    Task<CustomerCartView> PreviewAsync(
        IReadOnlyList<AnonymousCartPreviewInput> items,
        CancellationToken cancellationToken);
}

public sealed record AnonymousCartPreviewInput(Guid ProductId, int Quantity);

public sealed record CustomerCartView(
    Guid? CartId,
    long Version,
    IReadOnlyList<CustomerCartItemView> Items,
    decimal DisplayTotal)
{
    public int TotalQuantity => Items.Sum(item => item.Quantity);
}

public sealed record CustomerCartItemView(
    Guid ProductId,
    string DisplayName,
    string Slug,
    string BrandSlug,
    string PrimaryImageUrl,
    decimal CurrentUnitPrice,
    int Quantity,
    bool IsCurrentlyAvailable);
