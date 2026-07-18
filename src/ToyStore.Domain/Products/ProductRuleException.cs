namespace ToyStore.Domain.Products;

public sealed class ProductRuleException(ProductRule rule) : Exception(rule.ToString())
{
    public ProductRule Rule { get; } = rule;
}
