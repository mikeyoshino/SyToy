using Microsoft.EntityFrameworkCore;
using ToyStore.Application.Cart;
using ToyStore.Domain.Products;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class CartReader(IDbContextFactory<ApplicationDbContext> contextFactory) : ICartReader
{
    public async Task<CustomerCartView> GetAsync(string customerId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var cart = await db.Carts.AsNoTracking().Include(current => current.Items)
            .SingleOrDefaultAsync(current => current.CustomerId == customerId, cancellationToken);
        if (cart is null) return new(null, 0, [], 0);
        return await ReadAsync(db, cart.Items.Select(item =>
            new AnonymousCartPreviewInput(item.ProductId, item.Quantity)).ToArray(),
            cart.Id, cart.Version, publicProductsOnly: false, cancellationToken);
    }

    public async Task<CustomerCartView> PreviewAsync(
        IReadOnlyList<AnonymousCartPreviewInput> items,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await ReadAsync(db, items, null, 0, publicProductsOnly: true, cancellationToken);
    }

    private static async Task<CustomerCartView> ReadAsync(
        ApplicationDbContext db,
        IReadOnlyList<AnonymousCartPreviewInput> cartItems,
        Guid? cartId,
        long version,
        bool publicProductsOnly,
        CancellationToken cancellationToken)
    {
        var productIds = cartItems.Select(item => item.ProductId).ToArray();
        var productQuery = db.Products.AsNoTracking().Include(product => product.Images)
            .Where(product => productIds.Contains(product.Id));
        if (publicProductsOnly)
            productQuery = productQuery.Where(product => product.Status == ProductStatus.Published
                && product.SaleType == SaleType.InStock);
        var products = await productQuery
            .ToDictionaryAsync(product => product.Id, cancellationToken);
        var items = cartItems.OrderBy(item => item.ProductId).Select(item =>
        {
            if (!products.TryGetValue(item.ProductId, out var product))
                return new CustomerCartItemView(item.ProductId, "สินค้าไม่พร้อมใช้งาน", string.Empty,
                    string.Empty, 0, item.Quantity, false);
            var available = product.Status == ProductStatus.Published && product.SaleType == SaleType.InStock;
            var price = available ? product.InStockOffer!.Price.Amount : 0;
            var image = product.Images.OrderBy(value => value.SortOrder).FirstOrDefault()?.CardImageUrl ?? string.Empty;
            return new CustomerCartItemView(product.Id, product.DisplayName, product.Slug,
                image, price, item.Quantity, available);
        }).ToArray();
        return new(cartId, version, items,
            items.Sum(item => item.CurrentUnitPrice * item.Quantity));
    }
}
