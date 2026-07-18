using ToyStore.Domain.Carts;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;

namespace ToyStore.UnitTests.Domain.Carts;

public sealed class CartTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 17, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AnonymousItemKeepsOnlyUntrustedIdentityAndPositiveBoundedQuantity()
    {
        var productId = Guid.NewGuid();

        var item = AnonymousCartItem.Create(productId, 2);

        Assert.Equal(productId, item.ProductId);
        Assert.Equal(2, item.Quantity);
        Assert.Equal([nameof(AnonymousCartItem.ProductId), nameof(AnonymousCartItem.Quantity)],
            typeof(AnonymousCartItem).GetProperties().Select(property => property.Name).Order());
        AssertRule(CartRule.ProductIdentityRequired, () => AnonymousCartItem.Create(Guid.Empty, 1));
        AssertRule(CartRule.QuantityMustBePositive, () => AnonymousCartItem.Create(productId, 0));
        AssertRule(CartRule.QuantityMustBePositive, () => AnonymousCartItem.Create(productId, -1));
        AssertRule(CartRule.QuantityExceedsLimit,
            () => AnonymousCartItem.Create(productId, CartLimits.MaximumQuantityPerItem + 1));
    }

    [Fact]
    public void CreateRequiresStableCartCustomerIdentityAndUtcAudit()
    {
        AssertRule(CartRule.CartIdentityRequired, () => Cart.Create(Guid.Empty, "customer", Now));
        AssertRule(CartRule.CustomerIdentityRequired, () => Cart.Create(Guid.NewGuid(), " ", Now));
        AssertRule(CartRule.CustomerIdentityTooLong,
            () => Cart.Create(Guid.NewGuid(), new string('x', CartLimits.CustomerIdentityLength + 1), Now));
        AssertRule(CartRule.AuditInstantMustBeUtc,
            () => Cart.Create(Guid.NewGuid(), "customer", Now.ToOffset(TimeSpan.FromHours(7))));

        var cart = Cart.Create(Guid.NewGuid(), "  customer-1  ", Now);

        Assert.Equal("customer-1", cart.CustomerId);
        Assert.Equal(1, cart.Version);
        Assert.Empty(cart.Items);
        Assert.Equal(Now, cart.CreatedAtUtc);
        Assert.Equal(Now, cart.UpdatedAtUtc);
    }

    [Fact]
    public void AddAcceptsOnlyPublishedInStockAndCombinesWithoutPriceOrStockPromise()
    {
        var product = PublishedInStock();
        var cart = Cart.Create(Guid.NewGuid(), "customer-1", Now);

        cart.Add(product, 2, cart.Version, Now.AddMinutes(1));
        cart.Add(product, 3, cart.Version, Now.AddMinutes(2));

        var item = Assert.Single(cart.Items);
        Assert.Equal(product.Id, item.ProductId);
        Assert.Equal(5, item.Quantity);
        Assert.Equal(3, cart.Version);
        Assert.Equal([nameof(CartItem.ProductId), nameof(CartItem.Quantity)],
            typeof(CartItem).GetProperties().Select(property => property.Name).Order());
    }

    [Fact]
    public void AddRejectsDraftArchivedAndPreOrderProductsAtomically()
    {
        var draft = InStockDraft("draft-product");
        var archived = PublishedInStock("archived-product");
        archived.Archive(archived.Version, Now.AddMinutes(1), "test");
        var preOrder = PreOrderDraft();
        var cart = Cart.Create(Guid.NewGuid(), "customer-1", Now);

        AssertRule(CartRule.ProductMustBePublishedInStock,
            () => cart.Add(draft, 1, cart.Version, Now.AddMinutes(2)));
        AssertRule(CartRule.ProductMustBePublishedInStock,
            () => cart.Add(archived, 1, cart.Version, Now.AddMinutes(2)));
        AssertRule(CartRule.ProductMustBePublishedInStock,
            () => cart.Add(preOrder, 1, cart.Version, Now.AddMinutes(2)));
        Assert.Empty(cart.Items);
        Assert.Equal(1, cart.Version);
        Assert.Equal(Now, cart.UpdatedAtUtc);
    }

    [Fact]
    public void QuantityRulesVersionAndAuditFailuresLeaveCartUnchanged()
    {
        var product = PublishedInStock();
        var cart = Cart.Create(Guid.NewGuid(), "customer-1", Now);
        cart.Add(product, CartLimits.MaximumQuantityPerItem, cart.Version, Now.AddMinutes(1));
        var version = cart.Version;
        var updated = cart.UpdatedAtUtc;

        AssertRule(CartRule.QuantityMustBePositive,
            () => cart.SetQuantity(product.Id, 0, cart.Version, Now.AddMinutes(2)));
        AssertRule(CartRule.QuantityExceedsLimit,
            () => cart.Add(product, 1, cart.Version, Now.AddMinutes(2)));
        AssertRule(CartRule.ConcurrencyVersionMismatch,
            () => cart.SetQuantity(product.Id, 1, cart.Version - 1, Now.AddMinutes(2)));
        AssertRule(CartRule.AuditTimeWentBackwards,
            () => cart.SetQuantity(product.Id, 1, cart.Version, Now));

        Assert.Equal(CartLimits.MaximumQuantityPerItem, Assert.Single(cart.Items).Quantity);
        Assert.Equal(version, cart.Version);
        Assert.Equal(updated, cart.UpdatedAtUtc);
    }

    [Fact]
    public void ChangeRemoveClearAndOwnershipProtectCustomerCart()
    {
        var first = PublishedInStock("first-product");
        var second = PublishedInStock("second-product");
        var cart = Cart.Create(Guid.NewGuid(), "customer-1", Now);
        cart.Add(first, 1, cart.Version, Now.AddMinutes(1));
        cart.Add(second, 2, cart.Version, Now.AddMinutes(2));

        cart.EnsureOwnedBy("customer-1");
        AssertRule(CartRule.CartOwnershipMismatch, () => cart.EnsureOwnedBy("customer-2"));
        AssertRule(CartRule.CartItemNotFound,
            () => cart.SetQuantity(Guid.NewGuid(), 1, cart.Version, Now.AddMinutes(3)));

        cart.SetQuantity(first.Id, 4, cart.Version, Now.AddMinutes(3));
        cart.Remove(second.Id, cart.Version, Now.AddMinutes(4));
        Assert.Single(cart.Items);
        Assert.Equal(4, cart.Items[0].Quantity);

        cart.Clear(cart.Version, Now.AddMinutes(5));
        Assert.Empty(cart.Items);
    }

    private static Product PublishedInStock(string slug = "published-product")
    {
        var product = InStockDraft(slug);
        product.Publish(product.Version, Now, "test");
        return product;
    }

    private static Product InStockDraft(string slug) => Product.CreateInStock(
        Guid.NewGuid(), "สินค้าทดสอบ", slug.Replace('-', ' '), "รายละเอียดสินค้า", slug,
        CatalogSeedIds.ArtToyCategory, Guid.NewGuid(), CatalogSeedIds.MarvelUniverse,
        InStockOffer.Create(Money.Create(1000)),
        [new ProductImageDefinition(Guid.NewGuid(), $"{slug}/primary.webp", $"/media/{slug}.webp", "ภาพสินค้า")],
        [], Now.AddMinutes(-1), "test");

    private static Product PreOrderDraft() => Product.CreatePreOrder(
        Guid.NewGuid(), "สินค้าพรีออเดอร์", "pre order", "รายละเอียดสินค้า", "pre-order",
        CatalogSeedIds.ArtToyCategory, Guid.NewGuid(), CatalogSeedIds.MarvelUniverse,
        PreOrderOffer.Create(Money.Create(2000), Money.Create(500), new DateOnly(2026, 12, 1),
            EstimatedArrival.Create(12, 2026), 10, 2, Now),
        [new ProductImageDefinition(Guid.NewGuid(), "pre-order/primary.webp", "/media/pre-order.webp", "ภาพสินค้า")],
        [], Now.AddMinutes(-1), "test");

    private static void AssertRule(CartRule expected, Action action) =>
        Assert.Equal(expected, Assert.Throws<CartRuleException>(action).Rule);
}
