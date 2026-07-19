using ToyStore.Domain.Checkouts;
using ToyStore.Domain.Products;

namespace ToyStore.Domain.Orders;

public enum PaymentStatus { DepositPaid, Paid, PartiallyRefunded, Refunded }
public enum FulfillmentStatus { AwaitingPreOrderArrival, AwaitingBalancePayment, ReadyToShip, Shipped, Cancelled }

public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    private Order()
    {
        Number = CustomerId = null!;
        Address = null!;
    }

    private Order(Guid id, string number, CheckoutAttempt checkout, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Number = number;
        CustomerId = checkout.CustomerId;
        CheckoutAttemptId = checkout.Id;
        SaleType = checkout.SaleType;
        PaymentStatus = checkout.SaleType == SaleType.PreOrder ? PaymentStatus.DepositPaid : PaymentStatus.Paid;
        FulfillmentStatus = checkout.SaleType == SaleType.PreOrder
            ? FulfillmentStatus.AwaitingPreOrderArrival
            : FulfillmentStatus.ReadyToShip;
        Address = checkout.Address;
        ShippingAmount = checkout.ShippingAmount;
        TotalPaid = checkout.PaymentAmount;
        _items.AddRange(checkout.Items.Select(item => OrderItem.From(Guid.NewGuid(), item)));
        CreatedAtUtc = createdAtUtc;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public string Number { get; private set; }
    public string CustomerId { get; private set; }
    public Guid CheckoutAttemptId { get; private set; }
    public SaleType SaleType { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; }
    public FulfillmentStatus FulfillmentStatus { get; private set; }
    public ShippingAddressSnapshot Address { get; private set; }
    public decimal ShippingAmount { get; private set; }
    public decimal TotalPaid { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();
    public OrderItem Item => _items.Count == 1 ? _items[0]
        : throw new InvalidOperationException("Order contains more than one item.");
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ShippedAtUtc { get; private set; }
    public long Version { get; private set; }

    public static Order CreatePreOrder(Guid id, string number, CheckoutAttempt checkout, DateTimeOffset createdAtUtc)
    {
        EnsureCanCreate(id, number, checkout, SaleType.PreOrder, createdAtUtc);
        return new(id, number.Trim(), checkout, createdAtUtc);
    }

    public static Order CreateInStock(Guid id, string number, CheckoutAttempt checkout, DateTimeOffset createdAtUtc)
    {
        EnsureCanCreate(id, number, checkout, SaleType.InStock, createdAtUtc);
        return new(id, number.Trim(), checkout, createdAtUtc);
    }

    public void MarkShipped(long expectedVersion, DateTimeOffset shippedAtUtc)
    {
        if (expectedVersion != Version)
            throw new InvalidOperationException("Order version is stale.");
        if (PaymentStatus != PaymentStatus.Paid || FulfillmentStatus != FulfillmentStatus.ReadyToShip)
            throw new InvalidOperationException("Only a paid ready-to-ship Order can be shipped.");
        if (shippedAtUtc.Offset != TimeSpan.Zero || shippedAtUtc < CreatedAtUtc)
            throw new ArgumentException("Shipment timestamp must be valid UTC.", nameof(shippedAtUtc));
        FulfillmentStatus = FulfillmentStatus.Shipped;
        ShippedAtUtc = shippedAtUtc;
        Version++;
    }

    private static void EnsureCanCreate(Guid id, string number, CheckoutAttempt checkout,
        SaleType saleType, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(number))
            throw new ArgumentException("Order identity is required.");
        if (checkout.Status != CheckoutAttemptStatus.AwaitingPayment || checkout.SaleType != saleType)
            throw new InvalidOperationException("Order requires a matching awaiting-payment checkout verified by the caller.");
        if (createdAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Order timestamp must be UTC.", nameof(createdAtUtc));
    }
}

public sealed class OrderItem
{
    private OrderItem()
    {
        DisplayName = EnglishName = ProductSlug = CategoryName = BrandName = UniverseName = PrimaryImageUrl = null!;
    }

    private OrderItem(Guid id, CheckoutAttemptItem item)
    {
        Id = id;
        ProductId = item.ProductId;
        SaleType = item.SaleType;
        DisplayName = item.DisplayName;
        EnglishName = item.EnglishName;
        ProductSlug = item.ProductSlug;
        CategoryName = item.CategoryName;
        BrandName = item.BrandName;
        UniverseName = item.UniverseName;
        PrimaryImageUrl = item.PrimaryImageUrl;
        Quantity = item.Quantity;
        FullPrice = item.UnitPrice;
        DepositAmount = item.DepositAmount;
        BalanceAmount = item.BalanceAmount;
        LinePaidAmount = item.LinePaymentAmount;
        PreOrderCloseAtUtc = item.PreOrderCloseAtUtc;
        EstimatedArrivalMonth = item.EstimatedArrivalMonth;
        EstimatedArrivalYear = item.EstimatedArrivalYear;
        BalancePaymentDays = item.BalancePaymentDays;
        DepositPolicy = item.DepositPolicy;
    }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public SaleType SaleType { get; private set; }
    public string DisplayName { get; private set; }
    public string EnglishName { get; private set; }
    public string ProductSlug { get; private set; }
    public string CategoryName { get; private set; }
    public string BrandName { get; private set; }
    public string UniverseName { get; private set; }
    public string PrimaryImageUrl { get; private set; }
    public int Quantity { get; private set; }
    public decimal FullPrice { get; private set; }
    public decimal DepositAmount { get; private set; }
    public decimal BalanceAmount { get; private set; }
    public decimal LinePaidAmount { get; private set; }
    public DateTimeOffset? PreOrderCloseAtUtc { get; private set; }
    public int? EstimatedArrivalMonth { get; private set; }
    public int? EstimatedArrivalYear { get; private set; }
    public int? BalancePaymentDays { get; private set; }
    public string? DepositPolicy { get; private set; }

    internal static OrderItem From(Guid id, CheckoutAttemptItem item)
    {
        if (id == Guid.Empty) throw new ArgumentException("Order item identity is required.", nameof(id));
        return new(id, item);
    }
}
