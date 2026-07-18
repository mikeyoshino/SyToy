namespace ToyStore.Domain.Products;

public sealed record EstimatedArrival
{
    private EstimatedArrival()
    {
    }

    private EstimatedArrival(int month, int year)
    {
        Month = month;
        Year = year;
    }

    public int Month { get; private set; }

    public int Year { get; private set; }

    public static EstimatedArrival Create(int month, int year)
    {
        if (month is < 1 or > 12 || year is < 1 or > 9999)
        {
            throw new ProductRuleException(ProductRule.EstimatedArrivalInvalid);
        }

        return new EstimatedArrival(month, year);
    }
}
