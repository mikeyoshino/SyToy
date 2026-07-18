using System.Text.Json;
using Microsoft.JSInterop;
using ToyStore.Domain.Carts;

namespace ToyStore.Web.Components.Cart;

public sealed class AnonymousCartBrowserStore(IJSRuntime jsRuntime) : IAsyncDisposable
{
    internal const int MaximumStoredItems = 100;
    private readonly CancellationTokenSource disposalTokenSource = new();
    private IJSObjectReference? module;

    public async Task<AnonymousCartSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        var browser = await ModuleAsync(cancellationToken);
        var stored = await browser.InvokeAsync<JsonElement?>(
            "load", cancellationToken);
        if (TryParseStored(stored, out var snapshot)) return snapshot;
        await browser.InvokeVoidAsync("clear", cancellationToken);
        return Normalize(null);
    }

    public async Task SaveAsync(AnonymousCartSnapshot snapshot, CancellationToken cancellationToken)
    {
        var browser = await ModuleAsync(cancellationToken);
        await browser.InvokeVoidAsync("save", cancellationToken, Normalize(snapshot));
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        var browser = await ModuleAsync(cancellationToken);
        await browser.InvokeVoidAsync("clear", cancellationToken);
    }

    private async Task<IJSObjectReference> ModuleAsync(CancellationToken cancellationToken)
    {
        if (module is not null) return module;
        module = await jsRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            cancellationToken,
            "./Components/Cart/StoreCartDrawer.razor.js");
        return module;
    }

    internal static AnonymousCartSnapshot Normalize(AnonymousCartSnapshot? snapshot)
    {
        var items = snapshot?.Items?
            .Where(item => item.ProductId != Guid.Empty && item.Quantity > 0)
            .GroupBy(item => item.ProductId)
            .Select(group => new AnonymousCartLine(
                group.Key,
                (int)Math.Min(group.Sum(item => (long)item.Quantity), CartLimits.MaximumQuantityPerItem)))
            .OrderBy(item => item.ProductId)
            .Take(MaximumStoredItems)
            .ToArray() ?? [];
        return new(
            snapshot is { MergeOperationId: not null }
                && Guid.TryParse(snapshot.MergeOperationId, out var operationId)
                && operationId != Guid.Empty
                    ? operationId.ToString("D")
                    : Guid.NewGuid().ToString("D"),
            items);
    }

    internal static bool TryParseStored(
        JsonElement? stored,
        out AnonymousCartSnapshot snapshot)
    {
        snapshot = Normalize(null);
        if (stored is null || stored.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return true;
        var root = stored.Value;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("mergeOperationId", out var operationProperty)
            || operationProperty.ValueKind != JsonValueKind.String
            || !Guid.TryParse(operationProperty.GetString(), out var operationId)
            || operationId == Guid.Empty
            || !root.TryGetProperty("items", out var itemsProperty)
            || itemsProperty.ValueKind != JsonValueKind.Array
            || itemsProperty.GetArrayLength() > MaximumStoredItems)
            return false;

        var items = new List<AnonymousCartLine>();
        foreach (var item in itemsProperty.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("productId", out var productProperty)
                || productProperty.ValueKind != JsonValueKind.String
                || !Guid.TryParse(productProperty.GetString(), out var productId)
                || productId == Guid.Empty
                || !item.TryGetProperty("quantity", out var quantityProperty)
                || !quantityProperty.TryGetInt32(out var quantity)
                || quantity is < 1 or > CartLimits.MaximumQuantityPerItem)
                return false;
            items.Add(new(productId, quantity));
        }

        snapshot = Normalize(new(operationId.ToString("D"), items));
        return true;
    }

    internal static bool CanAddDistinctProduct(
        AnonymousCartSnapshot snapshot,
        Guid productId) =>
        snapshot.Items?.Any(item => item.ProductId == productId) == true
        || (snapshot.Items?.Count ?? 0) < MaximumStoredItems;

    public async ValueTask DisposeAsync()
    {
        await disposalTokenSource.CancelAsync();
        disposalTokenSource.Dispose();
        if (module is not null)
        {
            try { await module.DisposeAsync(); }
            catch (JSDisconnectedException) { }
            catch (ObjectDisposedException) { }
        }
    }
}

public sealed record AnonymousCartSnapshot(
    string? MergeOperationId,
    IReadOnlyList<AnonymousCartLine>? Items);

public sealed record AnonymousCartLine(Guid ProductId, int Quantity);
