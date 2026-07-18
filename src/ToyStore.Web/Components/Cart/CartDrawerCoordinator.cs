namespace ToyStore.Web.Components.Cart;

public sealed class CartDrawerCoordinator
{
    private Func<Guid, Task<bool>>? addProduct;
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public event Action? Changed;

    public bool IsOpen { get; private set; }

    public int TotalQuantity { get; private set; }

    internal void Attach(Func<Guid, Task<bool>> handler) => addProduct = handler;

    internal void Detach(Func<Guid, Task<bool>> handler)
    {
        if (addProduct == handler) addProduct = null;
    }

    public async Task<bool> AddProductAsync(Guid productId)
    {
        if (productId == Guid.Empty || addProduct is null) return false;
        return await addProduct(productId);
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        Changed?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Changed?.Invoke();
    }

    internal void SetOpen(bool value)
    {
        if (value) Open(); else Close();
    }

    internal void SetTotalQuantity(int value)
    {
        var normalized = Math.Max(0, value);
        if (TotalQuantity == normalized) return;
        TotalQuantity = normalized;
        Changed?.Invoke();
    }

    internal void MarkReady() => ready.TrySetResult();

    public Task WaitUntilReadyAsync(CancellationToken cancellationToken) =>
        ready.Task.WaitAsync(cancellationToken);
}
