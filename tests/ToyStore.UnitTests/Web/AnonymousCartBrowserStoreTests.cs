using System.Text.Json;
using ToyStore.Web.Components.Cart;

namespace ToyStore.UnitTests.Web;

public sealed class AnonymousCartBrowserStoreTests
{
    [Fact]
    public void NormalizeTreatsBrowserDataAsUntrustedAndClampsDuplicates()
    {
        var productId = Guid.NewGuid();

        var normalized = AnonymousCartBrowserStore.Normalize(new(
            "not-a-guid",
        [
            new(productId, 70),
            new(productId, 70),
            new(Guid.Empty, 1),
            new(Guid.NewGuid(), -1),
        ]));

        Assert.True(Guid.TryParse(normalized.MergeOperationId, out var operationId));
        Assert.NotEqual(Guid.Empty, operationId);
        var item = Assert.Single(normalized.Items!);
        Assert.Equal(productId, item.ProductId);
        Assert.Equal(99, item.Quantity);
    }

    [Fact]
    public async Task CoordinatorTracksCountOpenStateAndDelegatesAddWithoutNavigation()
    {
        var coordinator = new CartDrawerCoordinator();
        var changed = 0;
        var productId = Guid.NewGuid();
        coordinator.Changed += () => changed++;
        coordinator.Attach(id => Task.FromResult(id == productId));

        coordinator.SetTotalQuantity(3);
        coordinator.Open();
        var added = await coordinator.AddProductAsync(productId);
        coordinator.Close();

        Assert.True(added);
        Assert.Equal(3, coordinator.TotalQuantity);
        Assert.False(coordinator.IsOpen);
        Assert.Equal(3, changed);
    }

    [Theory]
    [InlineData("{\"mergeOperationId\":\"not-a-guid\",\"items\":[]}")]
    [InlineData("{\"mergeOperationId\":\"10000000-0000-4000-8000-000000000001\",\"items\":{}}")]
    [InlineData("{\"mergeOperationId\":\"10000000-0000-4000-8000-000000000001\",\"items\":[{\"productId\":\"20000000-0000-4000-8000-000000000001\",\"quantity\":999999999999}]}")]
    public void StoredPayloadSchemaIsValidatedBeforeTypedMaterialization(string json)
    {
        using var document = JsonDocument.Parse(json);

        var valid = AnonymousCartBrowserStore.TryParseStored(document.RootElement, out var snapshot);

        Assert.False(valid);
        Assert.Empty(snapshot.Items!);
    }

    [Fact]
    public void DistinctItemLimitRejectsOnlyTheNewHundredAndFirstProduct()
    {
        var items = Enumerable.Range(0, AnonymousCartBrowserStore.MaximumStoredItems)
            .Select(_ => new AnonymousCartLine(Guid.NewGuid(), 1))
            .ToArray();
        var snapshot = new AnonymousCartSnapshot(Guid.NewGuid().ToString("D"), items);

        Assert.True(AnonymousCartBrowserStore.CanAddDistinctProduct(snapshot, items[0].ProductId));
        Assert.False(AnonymousCartBrowserStore.CanAddDistinctProduct(snapshot, Guid.NewGuid()));
    }
}
