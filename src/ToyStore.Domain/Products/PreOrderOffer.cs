namespace ToyStore.Domain.Products;

public sealed record PreOrderOffer
{
    private static readonly TimeZoneInfo BangkokTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");

    private PreOrderOffer()
    {
        FullPrice = null!;
        DepositAmount = null!;
    }

    private PreOrderOffer(
        Money fullPrice,
        Money depositAmount,
        DateTimeOffset closeAtUtc,
        EstimatedArrival estimatedArrival,
        int totalCapacity,
        int maxPerCustomer,
        int balancePaymentDays)
    {
        FullPrice = fullPrice;
        DepositAmount = depositAmount;
        CloseAtUtc = closeAtUtc;
        EstimatedArrivalMonth = estimatedArrival.Month;
        EstimatedArrivalYear = estimatedArrival.Year;
        TotalCapacity = totalCapacity;
        MaxPerCustomer = maxPerCustomer;
        BalancePaymentDays = balancePaymentDays;
    }

    public Money FullPrice { get; private set; }

    public Money DepositAmount { get; private set; }

    public Money BalanceAmount => Money.Create(FullPrice.Amount - DepositAmount.Amount);

    public DateTimeOffset CloseAtUtc { get; private set; }

    public EstimatedArrival EstimatedArrival =>
        EstimatedArrival.Create(EstimatedArrivalMonth, EstimatedArrivalYear);

    private int EstimatedArrivalMonth { get; set; }

    private int EstimatedArrivalYear { get; set; }

    public int TotalCapacity { get; private set; }

    public int MaxPerCustomer { get; private set; }

    public int BalancePaymentDays { get; private set; }

    public static PreOrderOffer Create(
        Money fullPrice,
        Money depositAmount,
        DateOnly closeDate,
        EstimatedArrival estimatedArrival,
        int totalCapacity,
        int maxPerCustomer,
        DateTimeOffset nowUtc,
        int balancePaymentDays = 7)
    {
        if (fullPrice is null || fullPrice.Amount == 0)
        {
            throw new ProductRuleException(ProductRule.PreOrderFullPriceMustBePositive);
        }

        if (depositAmount is null || depositAmount.Amount == 0)
        {
            throw new ProductRuleException(ProductRule.PreOrderDepositMustBePositive);
        }

        if (depositAmount.Amount >= fullPrice.Amount)
        {
            throw new ProductRuleException(ProductRule.PreOrderDepositMustBeBelowFullPrice);
        }

        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ProductRuleException(ProductRule.UtcInstantRequired);
        }

        var closeAtUtc = ConvertBangkokCloseToUtc(closeDate);
        if (closeAtUtc <= nowUtc)
        {
            throw new ProductRuleException(ProductRule.PreOrderCloseMustBeFuture);
        }

        if (estimatedArrival is null)
        {
            throw new ProductRuleException(ProductRule.EstimatedArrivalInvalid);
        }

        if (estimatedArrival.Year < closeDate.Year ||
            (estimatedArrival.Year == closeDate.Year && estimatedArrival.Month < closeDate.Month))
        {
            throw new ProductRuleException(ProductRule.EstimatedArrivalBeforeClose);
        }

        if (totalCapacity <= 0)
        {
            throw new ProductRuleException(ProductRule.PreOrderCapacityMustBePositive);
        }

        if (maxPerCustomer <= 0)
        {
            throw new ProductRuleException(ProductRule.PreOrderMaxPerCustomerMustBePositive);
        }

        if (maxPerCustomer > totalCapacity)
        {
            throw new ProductRuleException(ProductRule.PreOrderMaxPerCustomerExceedsCapacity);
        }

        if (balancePaymentDays <= 0)
        {
            throw new ProductRuleException(ProductRule.PreOrderBalancePaymentDaysMustBePositive);
        }

        return new PreOrderOffer(
            fullPrice,
            depositAmount,
            closeAtUtc,
            estimatedArrival,
            totalCapacity,
            maxPerCustomer,
            balancePaymentDays);
    }

    private static DateTimeOffset ConvertBangkokCloseToUtc(DateOnly closeDate)
    {
        var localClose = DateTime.SpecifyKind(
            closeDate.ToDateTime(new TimeOnly(23, 59, 59)),
            DateTimeKind.Unspecified);
        var utcClose = TimeZoneInfo.ConvertTimeToUtc(localClose, BangkokTimeZone);

        return new DateTimeOffset(utcClose, TimeSpan.Zero);
    }
}
