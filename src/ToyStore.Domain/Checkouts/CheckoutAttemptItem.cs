using ToyStore.Domain.Products;

namespace ToyStore.Domain.Checkouts;

public sealed class CheckoutAttemptItem
{
    private CheckoutAttemptItem()
    {
        DisplayName = EnglishName = ProductSlug = CategoryName = BrandName = UniverseName = PrimaryImageUrl = null!;
    }

    private CheckoutAttemptItem(Guid id, Guid productId, Guid resourceId, Guid reservationId,
        SaleType saleType, int quantity, string displayName, string englishName, string productSlug,
        string categoryName, string brandName, string universeName, string primaryImageUrl,
        decimal unitPrice, decimal depositAmount, DateTimeOffset? preOrderCloseAtUtc,
        int? estimatedArrivalMonth, int? estimatedArrivalYear, int? balancePaymentDays,
        string? depositPolicy)
    {
        Id = id;
        ProductId = productId;
        ResourceId = resourceId;
        ReservationId = reservationId;
        SaleType = saleType;
        Quantity = quantity;
        DisplayName = Prepare(displayName);
        EnglishName = Prepare(englishName);
        ProductSlug = Prepare(productSlug);
        CategoryName = Prepare(categoryName);
        BrandName = Prepare(brandName);
        UniverseName = Prepare(universeName);
        PrimaryImageUrl = Prepare(primaryImageUrl);
        UnitPrice = unitPrice;
        DepositAmount = depositAmount;
        BalanceAmount = saleType == SaleType.PreOrder ? unitPrice - depositAmount : 0;
        LinePaymentAmount = (saleType == SaleType.PreOrder ? depositAmount : unitPrice) * quantity;
        PreOrderCloseAtUtc = preOrderCloseAtUtc;
        EstimatedArrivalMonth = estimatedArrivalMonth;
        EstimatedArrivalYear = estimatedArrivalYear;
        BalancePaymentDays = balancePaymentDays;
        DepositPolicy = depositPolicy;
    }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid ResourceId { get; private set; }
    public Guid ReservationId { get; private set; }
    public SaleType SaleType { get; private set; }
    public int Quantity { get; private set; }
    public string DisplayName { get; private set; }
    public string EnglishName { get; private set; }
    public string ProductSlug { get; private set; }
    public string CategoryName { get; private set; }
    public string BrandName { get; private set; }
    public string UniverseName { get; private set; }
    public string PrimaryImageUrl { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DepositAmount { get; private set; }
    public decimal BalanceAmount { get; private set; }
    public decimal LinePaymentAmount { get; private set; }
    public DateTimeOffset? PreOrderCloseAtUtc { get; private set; }
    public int? EstimatedArrivalMonth { get; private set; }
    public int? EstimatedArrivalYear { get; private set; }
    public int? BalancePaymentDays { get; private set; }
    public string? DepositPolicy { get; private set; }

    internal static CheckoutAttemptItem CreateInStock(Guid id, Guid productId, Guid inventoryItemId,
        Guid reservationId, int quantity, string displayName, string englishName, string productSlug,
        string categoryName, string brandName, string universeName, string primaryImageUrl,
        decimal unitPrice)
    {
        Validate(id, productId, inventoryItemId, reservationId, quantity, unitPrice);
        return new(id, productId, inventoryItemId, reservationId, SaleType.InStock, quantity,
            displayName, englishName, productSlug, categoryName, brandName, universeName,
            primaryImageUrl, unitPrice, 0, null, null, null, null, null);
    }

    internal static CheckoutAttemptItem CreatePreOrder(Guid id, Guid productId, Guid capacityId,
        Guid reservationId, int quantity, string displayName, string englishName, string productSlug,
        string categoryName, string brandName, string universeName, string primaryImageUrl,
        decimal fullPrice, decimal depositAmount, DateTimeOffset closeAtUtc,
        int arrivalMonth, int arrivalYear, int balancePaymentDays)
    {
        Validate(id, productId, capacityId, reservationId, quantity, fullPrice);
        if (depositAmount <= 0 || depositAmount >= fullPrice)
            throw new ArgumentException("Pre-order deposit is invalid.");
        return new(id, productId, capacityId, reservationId, SaleType.PreOrder, quantity,
            displayName, englishName, productSlug, categoryName, brandName, universeName,
            primaryImageUrl, fullPrice, depositAmount, closeAtUtc, arrivalMonth, arrivalYear,
            balancePaymentDays, "NonRefundableOnCustomerCancellationOrBalanceOverdue");
    }

    private static void Validate(Guid id, Guid productId, Guid resourceId, Guid reservationId,
        int quantity, decimal unitPrice)
    {
        if (id == Guid.Empty || productId == Guid.Empty || resourceId == Guid.Empty || reservationId == Guid.Empty
            || quantity <= 0 || unitPrice <= 0)
            throw new ArgumentException("Checkout item identity, quantity and price are required.");
    }

    private static string Prepare(string value) => string.IsNullOrWhiteSpace(value)
        ? throw new ArgumentException("Checkout item snapshot text is required.")
        : value.Trim();
}
