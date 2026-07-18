using ToyStore.Domain.Products;

namespace ToyStore.Domain.Checkouts;

public enum CheckoutAttemptStatus
{
    AwaitingProvider,
    AwaitingPayment,
    Completed,
    Expired,
}

public sealed class CheckoutAttempt
{
    private readonly List<CheckoutAttemptItem> _items = [];

    private CheckoutAttempt()
    {
        CustomerId = Currency = IdempotencyKey = null!;
        Address = null!;
    }

    private CheckoutAttempt(
        Guid id,
        string customerId,
        SaleType saleType,
        IReadOnlyCollection<CheckoutAttemptItem> items,
        ShippingAddressSnapshot address,
        string idempotencyKey,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        Id = id;
        CustomerId = customerId;
        SaleType = saleType;
        _items.AddRange(items);
        Address = address;
        ShippingAmount = 0;
        PaymentAmount = items.Sum(item => item.LinePaymentAmount);
        Currency = Money.Currency;
        IdempotencyKey = idempotencyKey;
        CreatedAtUtc = UpdatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        Status = CheckoutAttemptStatus.AwaitingProvider;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public string CustomerId { get; private set; }
    public SaleType SaleType { get; private set; }
    public IReadOnlyList<CheckoutAttemptItem> Items => _items.AsReadOnly();
    public ShippingAddressSnapshot Address { get; private set; }
    public decimal ShippingAmount { get; private set; }
    public decimal PaymentAmount { get; private set; }
    public string Currency { get; private set; }
    public string IdempotencyKey { get; private set; }
    public string? ProviderSessionId { get; private set; }
    public CheckoutAttemptStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public long Version { get; private set; }

    // Compatibility projections for the direct Pre-order slice.
    public Guid ProductId => SinglePreOrderItem.ProductId;
    public Guid CapacityId => SinglePreOrderItem.ResourceId;
    public Guid ReservationId => SinglePreOrderItem.ReservationId;
    public int Quantity => SinglePreOrderItem.Quantity;
    public string DisplayName => SinglePreOrderItem.DisplayName;
    public string EnglishName => SinglePreOrderItem.EnglishName;
    public string ProductSlug => SinglePreOrderItem.ProductSlug;
    public string CategoryName => SinglePreOrderItem.CategoryName;
    public string BrandName => SinglePreOrderItem.BrandName;
    public string UniverseName => SinglePreOrderItem.UniverseName;
    public string PrimaryImageUrl => SinglePreOrderItem.PrimaryImageUrl;
    public decimal FullPrice => SinglePreOrderItem.UnitPrice;
    public decimal DepositAmount => SinglePreOrderItem.DepositAmount;
    public decimal BalanceAmount => SinglePreOrderItem.BalanceAmount;
    public DateTimeOffset PreOrderCloseAtUtc => SinglePreOrderItem.PreOrderCloseAtUtc!.Value;
    public int EstimatedArrivalMonth => SinglePreOrderItem.EstimatedArrivalMonth!.Value;
    public int EstimatedArrivalYear => SinglePreOrderItem.EstimatedArrivalYear!.Value;
    public int BalancePaymentDays => SinglePreOrderItem.BalancePaymentDays!.Value;
    public string DepositPolicy => SinglePreOrderItem.DepositPolicy!;

    private CheckoutAttemptItem SinglePreOrderItem =>
        SaleType == SaleType.PreOrder && _items.Count == 1
            ? _items[0]
            : throw new InvalidOperationException("Checkout is not a single-item Pre-order.");

    public static CheckoutAttempt CreatePreOrder(
        Guid id, string customerId, Guid productId, Guid capacityId, Guid reservationId,
        int quantity, string displayName, string englishName, string productSlug,
        string categoryName, string brandName, string universeName, string primaryImageUrl,
        decimal fullPrice, decimal depositAmount, DateTimeOffset preOrderCloseAtUtc,
        int estimatedArrivalMonth, int estimatedArrivalYear, int balancePaymentDays,
        ShippingAddressSnapshot address, string idempotencyKey,
        DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc)
    {
        ValidateCommon(id, customerId, idempotencyKey, createdAtUtc, expiresAtUtc);
        if (productId == Guid.Empty || capacityId == Guid.Empty || reservationId == Guid.Empty
            || quantity <= 0 || fullPrice <= 0 || depositAmount <= 0 || depositAmount >= fullPrice
            || preOrderCloseAtUtc.Offset != TimeSpan.Zero || createdAtUtc >= preOrderCloseAtUtc)
            throw new ArgumentException("Pre-order checkout item is invalid.");

        var item = CheckoutAttemptItem.CreatePreOrder(Guid.NewGuid(), productId, capacityId,
            reservationId, quantity, displayName, englishName, productSlug, categoryName,
            brandName, universeName, primaryImageUrl, fullPrice, depositAmount,
            preOrderCloseAtUtc, estimatedArrivalMonth, estimatedArrivalYear, balancePaymentDays);
        return new(id, customerId.Trim(), SaleType.PreOrder, [item], address,
            idempotencyKey.Trim(), createdAtUtc, expiresAtUtc);
    }

    public static CheckoutAttempt CreateInStock(
        Guid id,
        string customerId,
        IReadOnlyCollection<InStockCheckoutItemDefinition> itemDefinitions,
        ShippingAddressSnapshot address,
        string idempotencyKey,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        ValidateCommon(id, customerId, idempotencyKey, createdAtUtc, expiresAtUtc);
        if (itemDefinitions.Count == 0)
            throw new ArgumentException("In-stock checkout requires at least one item.", nameof(itemDefinitions));
        if (itemDefinitions.Select(item => item.ProductId).Distinct().Count() != itemDefinitions.Count)
            throw new ArgumentException("In-stock checkout products must be unique.", nameof(itemDefinitions));

        var items = itemDefinitions.Select(definition => CheckoutAttemptItem.CreateInStock(
            definition.ItemId, definition.ProductId, definition.InventoryItemId,
            definition.ReservationId, definition.Quantity, definition.DisplayName,
            definition.EnglishName, definition.ProductSlug, definition.CategoryName,
            definition.BrandName, definition.UniverseName, definition.PrimaryImageUrl,
            definition.UnitPrice)).ToArray();
        return new(id, customerId.Trim(), SaleType.InStock, items, address,
            idempotencyKey.Trim(), createdAtUtc, expiresAtUtc);
    }

    public void AttachProviderSession(string providerSessionId, DateTimeOffset changedAtUtc)
    {
        if (Status == CheckoutAttemptStatus.Completed)
            throw new InvalidOperationException("Completed checkout cannot change provider session.");
        var prepared = string.IsNullOrWhiteSpace(providerSessionId)
            ? throw new ArgumentException("Provider session is required.", nameof(providerSessionId))
            : providerSessionId.Trim();
        if (ProviderSessionId is not null && !string.Equals(ProviderSessionId, prepared, StringComparison.Ordinal))
            throw new InvalidOperationException("Checkout already belongs to another provider session.");
        if (ProviderSessionId is not null) return;
        Advance(changedAtUtc);
        ProviderSessionId = prepared;
        Status = CheckoutAttemptStatus.AwaitingPayment;
    }

    public void Complete(DateTimeOffset completedAtUtc)
    {
        if (Status == CheckoutAttemptStatus.Completed) return;
        if (Status != CheckoutAttemptStatus.AwaitingPayment || ProviderSessionId is null)
            throw new InvalidOperationException("Checkout is not awaiting verified payment.");
        Advance(completedAtUtc);
        Status = CheckoutAttemptStatus.Completed;
        CompletedAtUtc = completedAtUtc;
    }

    public void Expire(DateTimeOffset expiredAtUtc)
    {
        if (Status == CheckoutAttemptStatus.Expired) return;
        if (Status == CheckoutAttemptStatus.Completed)
            throw new InvalidOperationException("Completed checkout cannot expire.");
        if (expiredAtUtc < ExpiresAtUtc)
            throw new InvalidOperationException("Checkout cannot expire before its reservation window ends.");
        Advance(expiredAtUtc);
        Status = CheckoutAttemptStatus.Expired;
    }

    private static void ValidateCommon(Guid id, string customerId, string idempotencyKey,
        DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(customerId) || string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Checkout identity, customer and idempotency key are required.");
        if (createdAtUtc.Offset != TimeSpan.Zero || expiresAtUtc.Offset != TimeSpan.Zero || expiresAtUtc <= createdAtUtc)
            throw new ArgumentException("Checkout timestamps are invalid.");
    }

    private void Advance(DateTimeOffset changedAtUtc)
    {
        if (changedAtUtc.Offset != TimeSpan.Zero || changedAtUtc < UpdatedAtUtc)
            throw new ArgumentException("Checkout audit time is invalid.", nameof(changedAtUtc));
        Version = checked(Version + 1);
        UpdatedAtUtc = changedAtUtc;
    }
}

public sealed record InStockCheckoutItemDefinition(
    Guid ItemId,
    Guid ProductId,
    Guid InventoryItemId,
    Guid ReservationId,
    int Quantity,
    string DisplayName,
    string EnglishName,
    string ProductSlug,
    string CategoryName,
    string BrandName,
    string UniverseName,
    string PrimaryImageUrl,
    decimal UnitPrice);
