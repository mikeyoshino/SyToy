using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ToyStore.Domain.Carts;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Identity;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class CartPersistenceTests(PostgreSqlFixture postgreSql)
{
    private static readonly DateTimeOffset Now = new(2026, 7, 17, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CustomerCartRoundTripsOwnershipItemsAuditAndVersionWithoutCommercePromises()
    {
        await using var factory = await StartAndResetAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seeded = SeedCustomerAndPublishedProduct(db);
        var cart = Cart.Create(Guid.NewGuid(), seeded.CustomerId, Now);
        cart.Add(seeded.Product, 3, cart.Version, Now.AddMinutes(1));
        db.Carts.Add(cart);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var reloaded = await db.Carts.Include(current => current.Items).SingleAsync(
            current => current.Id == cart.Id, TestContext.Current.CancellationToken);

        Assert.Equal(seeded.CustomerId, reloaded.CustomerId);
        Assert.Equal(2, reloaded.Version);
        Assert.Equal(Now.AddMinutes(1), reloaded.UpdatedAtUtc);
        var item = Assert.Single(reloaded.Items);
        Assert.Equal(seeded.Product.Id, item.ProductId);
        Assert.Equal(3, item.Quantity);

        var itemColumns = await db.Database.SqlQueryRaw<string>(
            """
            SELECT column_name AS "Value"
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'CartItems'
            ORDER BY column_name
            """).ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["CartId", "ProductId", "Quantity"], itemColumns);
        Assert.DoesNotContain(itemColumns, column => column.Contains("Price", StringComparison.OrdinalIgnoreCase)
            || column.Contains("Stock", StringComparison.OrdinalIgnoreCase));
        var anonymousTableCount = await db.Database.SqlQueryRaw<int>(
            """
            SELECT COUNT(*)::integer AS "Value"
            FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = 'AnonymousCarts'
            """).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, anonymousTableCount);
    }

    [Fact]
    public async Task DatabaseProtectsOneCartPerCustomerProductIdentityAndQuantityBounds()
    {
        await using var factory = await StartAndResetAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seeded = SeedCustomerAndPublishedProduct(db);
        var first = Cart.Create(Guid.NewGuid(), seeded.CustomerId, Now);
        first.Add(seeded.Product, 1, first.Version, Now.AddMinutes(1));
        db.Carts.Add(first);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.Carts.Add(Cart.Create(Guid.NewGuid(), seeded.CustomerId, Now));
        await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        db.ChangeTracker.Clear();

        var quantityException = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"CartItems\" SET \"Quantity\" = {CartLimits.MaximumQuantityPerItem + 1} WHERE \"CartId\" = {first.Id}",
                TestContext.Current.CancellationToken));
        Assert.Equal(PostgresErrorCodes.CheckViolation, quantityException.SqlState);
    }

    [Fact]
    public async Task PersistedCartVersionRejectsConcurrentCustomerMutation()
    {
        await using var factory = await StartAndResetAsync();
        Guid cartId;
        Guid productId;
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var seeded = SeedCustomerAndPublishedProduct(db);
            productId = seeded.Product.Id;
            var cart = Cart.Create(Guid.NewGuid(), seeded.CustomerId, Now);
            cart.Add(seeded.Product, 1, cart.Version, Now.AddMinutes(1));
            cartId = cart.Id;
            db.Carts.Add(cart);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var firstDb = firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var first = await firstDb.Carts.Include(cart => cart.Items).SingleAsync(
            cart => cart.Id == cartId, TestContext.Current.CancellationToken);
        var second = await secondDb.Carts.Include(cart => cart.Items).SingleAsync(
            cart => cart.Id == cartId, TestContext.Current.CancellationToken);
        first.SetQuantity(productId, 2, first.Version, Now.AddMinutes(2));
        second.SetQuantity(productId, 3, second.Version, Now.AddMinutes(2));

        await firstDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => secondDb.SaveChangesAsync(TestContext.Current.CancellationToken));

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var durable = await verificationDb.Carts.AsNoTracking().Include(cart => cart.Items).SingleAsync(
            cart => cart.Id == cartId, TestContext.Current.CancellationToken);
        Assert.Equal(3, durable.Version);
        Assert.Equal(2, Assert.Single(durable.Items).Quantity);
    }

    private async Task<ToyStoreWebApplicationFactory> StartAndResetAsync()
    {
        var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        _ = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        return factory;
    }

    private static Seeded SeedCustomerAndPublishedProduct(ApplicationDbContext db)
    {
        var customerId = Guid.NewGuid().ToString("N");
        var email = $"{customerId}@example.test";
        db.Users.Add(new ApplicationUser
        {
            Id = customerId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
        });
        var brand = Brand.Create(Guid.NewGuid(), $"แบรนด์ {customerId}", $"Brand {customerId}",
            CatalogSlug.Create($"brand-{customerId}"), Now.AddMinutes(-2), "test");
        var product = Product.CreateInStock(
            Guid.NewGuid(), $"สินค้า {customerId}", $"Product {customerId}", "รายละเอียดสินค้า",
            $"product-{customerId}", CatalogSeedIds.ArtToyCategory, brand.Id, CatalogSeedIds.MarvelUniverse,
            InStockOffer.Create(Money.Create(1000)),
            [new ProductImageDefinition(Guid.NewGuid(), $"{customerId}/primary.webp", $"/media/{customerId}.webp", "ภาพสินค้า")],
            [], Now.AddMinutes(-1), "test");
        product.Publish(product.Version, Now, "test");
        db.Brands.Add(brand);
        db.Products.Add(product);
        return new Seeded(customerId, product);
    }

    private sealed record Seeded(string CustomerId, Product Product);
}
