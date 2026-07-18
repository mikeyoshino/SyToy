namespace ToyStore.Application.Cart;

public sealed record CartMutationResult(
    Guid CartId,
    long Version,
    long TotalQuantity,
    bool WasIdempotentRetry);
