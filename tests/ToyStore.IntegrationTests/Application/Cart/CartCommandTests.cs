using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application.Cart;
using ToyStore.Application.Cart.AddCartItem;
using ToyStore.Application.Cart.ChangeCartItemQuantity;
using ToyStore.Application.Cart.ClearCart;
using ToyStore.Application.Cart.GetAnonymousCartPreview;
using ToyStore.Application.Cart.GetCart;
using ToyStore.Application.Cart.MergeAnonymousCart;
using ToyStore.Application.Cart.RemoveCartItem;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Identity;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Application.Cart;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class CartCommandTests(PostgreSqlFixture postgreSql)
{
    private static readonly DateTimeOffset Now = new(2026, 7, 17, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddAndGetAreOwnerScopedIdempotentAndRejectChangedRetryIntent()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedAsync(factory);
        var operationId = Guid.NewGuid();
        var command = new AddCartItemCommand(operationId, seeded.Published.Id, 2, 0);

        var added = await AddAsync(factory, seeded.CustomerOne, command);
        var laterMutation = await AddAsync(factory, seeded.CustomerOne,
            new(Guid.NewGuid(), seeded.SecondPublished.Id, 1, added.Value.Version));
        var retry = await AddAsync(factory, seeded.CustomerOne, command);
        var conflict = await AddAsync(factory, seeded.CustomerOne, command with { Quantity = 3 });
        var otherOwner = await AddAsync(factory, seeded.CustomerTwo, command);
        var view = await GetAsync(factory, seeded.CustomerOne);
        var otherView = await GetAsync(factory, seeded.CustomerTwo);

        Assert.True(added.IsSuccess);
        Assert.False(added.Value.WasIdempotentRetry);
        Assert.True(retry.Value.WasIdempotentRetry);
        Assert.Equal(added.Value.Version, retry.Value.Version);
        Assert.Equal(added.Value.TotalQuantity, retry.Value.TotalQuantity);
        Assert.True(laterMutation.Value.Version > retry.Value.Version);
        Assert.Equal(CartErrors.OperationConflict, conflict.Error);
        Assert.Equal(CartErrors.OperationConflict, otherOwner.Error);
        Assert.Equal(2, view.Value.Items.Single(item => item.ProductId == seeded.Published.Id).Quantity);
        Assert.Equal(1, view.Value.Items.Single(item => item.ProductId == seeded.SecondPublished.Id).Quantity);
        Assert.Equal(3000, view.Value.DisplayTotal);
        Assert.Empty(otherView.Value.Items);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.CartOperations.CountAsync(
            operation => operation.Id == operationId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddAndChangeRejectUnavailableStaleAndOverLimitWithoutPartialWrites()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedAsync(factory);
        Assert.Equal(CartErrors.ProductUnavailable, (await AddAsync(factory, seeded.CustomerOne,
            new(Guid.NewGuid(), seeded.Draft.Id, 1, 0))).Error);
        Assert.Equal(CartErrors.ProductUnavailable, (await AddAsync(factory, seeded.CustomerOne,
            new(Guid.NewGuid(), seeded.Archived.Id, 1, 0))).Error);

        var added = await AddAsync(factory, seeded.CustomerOne,
            new(Guid.NewGuid(), seeded.Published.Id, 99, 0));
        var over = await AddAsync(factory, seeded.CustomerOne,
            new(Guid.NewGuid(), seeded.Published.Id, 1, added.Value.Version));
        var stale = await ChangeAsync(factory, seeded.CustomerOne,
            new(Guid.NewGuid(), seeded.Published.Id, 1, added.Value.Version - 1));

        Assert.Equal(CartErrors.QuantityExceedsLimit, over.Error);
        Assert.Equal(CartErrors.StaleVersion, stale.Error);
        var view = await GetAsync(factory, seeded.CustomerOne);
        Assert.Equal(99, Assert.Single(view.Value.Items).Quantity);
    }

    [Fact]
    public async Task ChangeRemoveClearReReadLifecycleAndPermitRemovingUnavailableItem()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedAsync(factory);
        var addFirst = await AddAsync(factory, seeded.CustomerOne,
            new(Guid.NewGuid(), seeded.Published.Id, 1, 0));
        var addSecond = await AddAsync(factory, seeded.CustomerOne,
            new(Guid.NewGuid(), seeded.SecondPublished.Id, 2, addFirst.Value.Version));
        var changed = await ChangeAsync(factory, seeded.CustomerOne,
            new(Guid.NewGuid(), seeded.Published.Id, 4, addSecond.Value.Version));
        await ArchiveAsync(factory, seeded.Published.Id);
        var unavailableView = await GetAsync(factory, seeded.CustomerOne);

        var unavailableChange = await ChangeAsync(factory, seeded.CustomerOne,
            new(Guid.NewGuid(), seeded.Published.Id, 3, changed.Value.Version));
        var removed = await RemoveAsync(factory, seeded.CustomerOne,
            new(Guid.NewGuid(), seeded.Published.Id, changed.Value.Version));
        var cleared = await ClearAsync(factory, seeded.CustomerOne,
            new(Guid.NewGuid(), removed.Value.Version));

        Assert.Equal(CartErrors.ProductUnavailable, unavailableChange.Error);
        var unavailableItem = unavailableView.Value.Items.Single(item => item.ProductId == seeded.Published.Id);
        Assert.False(unavailableItem.IsCurrentlyAvailable);
        Assert.Equal(0, unavailableItem.CurrentUnitPrice);
        Assert.True(removed.IsSuccess);
        Assert.True(cleared.IsSuccess);
        Assert.Equal(0, cleared.Value.TotalQuantity);
        Assert.Empty((await GetAsync(factory, seeded.CustomerOne)).Value.Items);
    }

    [Fact]
    public async Task MergeGroupsClampsRejectsUnavailableAndRetryNeverAppliesTwice()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedAsync(factory);
        _ = await AddAsync(factory, seeded.CustomerOne,
            new(Guid.NewGuid(), seeded.Published.Id, 90, 0));
        var operationId = Guid.NewGuid();
        var command = new MergeAnonymousCartCommand(operationId,
        [
            new(seeded.Published.Id, 8),
            new(seeded.Draft.Id, 1),
            new(seeded.Published.Id, 8),
            new(seeded.SecondPublished.Id, 2),
        ]);

        var merged = await MergeAsync(factory, seeded.CustomerOne, command);
        var laterMutation = await ChangeAsync(factory, seeded.CustomerOne,
            new(Guid.NewGuid(), seeded.SecondPublished.Id, 3, merged.Value.Cart.Version));
        var retry = await MergeAsync(factory, seeded.CustomerOne,
            command with { Items = command.Items.Reverse().ToArray() });
        var conflict = await MergeAsync(factory, seeded.CustomerOne,
            command with { Items = [new(seeded.Published.Id, 1)] });

        Assert.True(merged.IsSuccess);
        Assert.Single(merged.Value.RejectedItems, item => item.ProductId == seeded.Draft.Id);
        var clamp = Assert.Single(merged.Value.ClampedItems);
        Assert.Equal(106, clamp.RequestedQuantity);
        Assert.Equal(99, clamp.AppliedQuantity);
        Assert.True(retry.Value.Cart.WasIdempotentRetry);
        Assert.Equal(merged.Value.Cart.Version, retry.Value.Cart.Version);
        Assert.Equal(merged.Value.Cart.TotalQuantity, retry.Value.Cart.TotalQuantity);
        Assert.True(laterMutation.Value.Version > retry.Value.Cart.Version);
        Assert.Equal(merged.Value.RejectedItems, retry.Value.RejectedItems);
        Assert.Equal(merged.Value.ClampedItems, retry.Value.ClampedItems);
        Assert.Equal(CartErrors.OperationConflict, conflict.Error);
        var view = await GetAsync(factory, seeded.CustomerOne);
        Assert.Equal(99, view.Value.Items.Single(item => item.ProductId == seeded.Published.Id).Quantity);
        Assert.Equal(3, view.Value.Items.Single(item => item.ProductId == seeded.SecondPublished.Id).Quantity);
    }

    [Fact]
    public async Task MergeRejectsOperationIdAlreadyUsedByAnotherCartAction()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedAsync(factory);
        var operationId = Guid.NewGuid();
        var added = await AddAsync(factory, seeded.CustomerOne,
            new(operationId, seeded.Published.Id, 1, 0));

        var merge = await MergeAsync(factory, seeded.CustomerOne,
            new(operationId, [new(seeded.SecondPublished.Id, 1)]));

        Assert.True(added.IsSuccess);
        Assert.Equal(CartErrors.OperationConflict, merge.Error);
        var view = await GetAsync(factory, seeded.CustomerOne);
        Assert.Single(view.Value.Items);
        Assert.Equal(seeded.Published.Id, view.Value.Items[0].ProductId);
    }

    [Fact]
    public async Task ConcurrentAddsFromSameVersionHaveOneWinnerAndOneStale()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedAsync(factory);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;
        async Task<Result<CartMutationResult>> Run(int quantity)
        {
            if (Interlocked.Increment(ref entered) == 2) ready.TrySetResult();
            await ready.Task.WaitAsync(TestContext.Current.CancellationToken);
            return await AddAsync(factory, seeded.CustomerOne,
                new(Guid.NewGuid(), seeded.Published.Id, quantity, 0));
        }

        var results = await Task.WhenAll(Run(1), Run(2));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Equal(CartErrors.StaleVersion, Assert.Single(results, result => result.IsFailure).Error);
        var view = await GetAsync(factory, seeded.CustomerOne);
        Assert.True(Assert.Single(view.Value.Items).Quantity is 1 or 2);
    }

    [Fact]
    public async Task AnonymousPreviewReReadsCurrentPublishedInStockProductsAndPrices()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = new GetAnonymousCartPreviewHandler(
            scope.ServiceProvider.GetRequiredService<ICartReader>());

        var result = await handler.Handle(new GetAnonymousCartPreviewQuery(
        [
            new(seeded.Published.Id, 1),
            new(seeded.Published.Id, 2),
            new(seeded.Draft.Id, 1),
            new(Guid.NewGuid(), 1),
        ]), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var availableItem = result.Value.Items.Single(item => item.ProductId == seeded.Published.Id);
        Assert.Equal(3, availableItem.Quantity);
        Assert.Equal("cart-brand", availableItem.BrandSlug);
        Assert.Equal(3000, result.Value.DisplayTotal);
        Assert.Equal(2, result.Value.Items.Count(item => !item.IsCurrentlyAvailable));
        Assert.All(result.Value.Items.Where(item => !item.IsCurrentlyAvailable), item =>
        {
            Assert.Equal(0, item.CurrentUnitPrice);
            Assert.Equal("สินค้าไม่พร้อมใช้งาน", item.DisplayName);
            Assert.Empty(item.Slug);
            Assert.Empty(item.BrandSlug);
            Assert.Empty(item.PrimaryImageUrl);
        });
        Assert.Null(result.Value.CartId);
        Assert.Equal(0, result.Value.Version);
    }

    private async Task<ToyStoreWebApplicationFactory> StartAndResetAsync()
    {
        var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        _ = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        return factory;
    }

    private static async Task<Result<CartMutationResult>> AddAsync(
        ToyStoreWebApplicationFactory factory, string actor, AddCartItemCommand command)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = new AddCartItemHandler(
            scope.ServiceProvider.GetRequiredService<ICartMutationSessionFactory>(), new FixedTimeProvider());
        return await Authorize(command, actor, token => handler.Handle(command, token));
    }

    private static async Task<Result<CartMutationResult>> ChangeAsync(
        ToyStoreWebApplicationFactory factory, string actor, ChangeCartItemQuantityCommand command)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = new ChangeCartItemQuantityHandler(
            scope.ServiceProvider.GetRequiredService<ICartMutationSessionFactory>(), new FixedTimeProvider());
        return await Authorize(command, actor, token => handler.Handle(command, token));
    }

    private static async Task<Result<CartMutationResult>> RemoveAsync(
        ToyStoreWebApplicationFactory factory, string actor, RemoveCartItemCommand command)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = new RemoveCartItemHandler(
            scope.ServiceProvider.GetRequiredService<ICartMutationSessionFactory>(), new FixedTimeProvider());
        return await Authorize(command, actor, token => handler.Handle(command, token));
    }

    private static async Task<Result<CartMutationResult>> ClearAsync(
        ToyStoreWebApplicationFactory factory, string actor, ClearCartCommand command)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = new ClearCartHandler(
            scope.ServiceProvider.GetRequiredService<ICartMutationSessionFactory>(), new FixedTimeProvider());
        return await Authorize(command, actor, token => handler.Handle(command, token));
    }

    private static async Task<Result<MergeAnonymousCartResult>> MergeAsync(
        ToyStoreWebApplicationFactory factory, string actor, MergeAnonymousCartCommand command)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = new MergeAnonymousCartHandler(
            scope.ServiceProvider.GetRequiredService<ICartMutationSessionFactory>(), new FixedTimeProvider());
        return await Authorize(command, actor, token => handler.Handle(command, token));
    }

    private static async Task<Result<CustomerCartView>> GetAsync(
        ToyStoreWebApplicationFactory factory, string actor)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var query = new GetCartQuery();
        var handler = new GetCartHandler(scope.ServiceProvider.GetRequiredService<ICartReader>());
        return await Authorize(query, actor, token => handler.Handle(query, token));
    }

    private static Task<TResponse> Authorize<TRequest, TResponse>(
        TRequest request, string actor, Func<CancellationToken, Task<TResponse>> handler)
        where TRequest : notnull =>
        new AuthorizationBehavior<TRequest, TResponse>(new CustomerAuthorization(actor)).Handle(
            request, token => handler(token), TestContext.Current.CancellationToken);

    private static async Task ArchiveAsync(ToyStoreWebApplicationFactory factory, Guid productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await db.Products.SingleAsync(value => value.Id == productId,
            TestContext.Current.CancellationToken);
        product.Archive(product.Version, Now.AddMinutes(1), "test");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Seeded> SeedAsync(ToyStoreWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var customerOne = Guid.NewGuid().ToString("N");
        var customerTwo = Guid.NewGuid().ToString("N");
        db.Users.AddRange(User(customerOne), User(customerTwo));
        var brand = Brand.Create(Guid.NewGuid(), "แบรนด์ตะกร้า", "Cart Brand",
            CatalogSlug.Create("cart-brand"), Now.AddMinutes(-2), "test");
        Product Create(string slug) => Product.CreateInStock(Guid.NewGuid(), $"สินค้า {slug}", slug,
            "รายละเอียด", slug, CatalogSeedIds.ArtToyCategory, brand.Id, CatalogSeedIds.MarvelUniverse,
            InStockOffer.Create(Money.Create(1000)),
            [new ProductImageDefinition(Guid.NewGuid(), $"{slug}/main.webp", $"/media/{slug}.webp", "ภาพสินค้า")],
            [], Now.AddMinutes(-1), "test");
        var published = Create("cart-published"); published.Publish(published.Version, Now, "test");
        var second = Create("cart-second"); second.Publish(second.Version, Now, "test");
        var draft = Create("cart-draft");
        var archived = Create("cart-archived"); archived.Publish(archived.Version, Now, "test");
        archived.Archive(archived.Version, Now.AddMinutes(1), "test");
        db.Brands.Add(brand); db.Products.AddRange(published, second, draft, archived);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new(customerOne, customerTwo, published, second, draft, archived);
    }

    private static ApplicationUser User(string id)
    {
        var email = $"{id}@example.test";
        return new()
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
    }

    private sealed record Seeded(string CustomerOne, string CustomerTwo, Product Published,
        Product SecondPublished, Product Draft, Product Archived);
    private sealed class FixedTimeProvider : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class CustomerAuthorization(string actor) : ICurrentUserAuthorization
    {
        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(string policyName, CancellationToken cancellationToken) =>
            Task.FromResult(new CurrentUserAuthorizationResult(true,
                policyName == PolicyNames.CanUseCustomerCart, actor));
    }
}
