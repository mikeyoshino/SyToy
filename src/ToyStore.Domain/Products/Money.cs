namespace ToyStore.Domain.Products;

public sealed record Money
{
    private Money()
    {
    }

    private Money(decimal amount)
    {
        Amount = amount;
    }

    public const string Currency = "THB";

    public decimal Amount { get; private set; }

    public static Money Create(decimal amount)
    {
        if (amount < 0)
        {
            throw new ProductRuleException(ProductRule.MoneyCannotBeNegative);
        }

        return new Money(amount);
    }
}
