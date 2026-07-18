namespace ToyStore.Domain.Products;

public sealed record InStockOffer
{
    private InStockOffer()
    {
        Price = null!;
    }

    private InStockOffer(Money price)
    {
        Price = price;
    }

    public Money Price { get; private set; }

    public static InStockOffer Create(Money price)
    {
        if (price is null || price.Amount == 0)
        {
            throw new ProductRuleException(ProductRule.InStockPriceMustBePositive);
        }

        return new InStockOffer(price);
    }
}
