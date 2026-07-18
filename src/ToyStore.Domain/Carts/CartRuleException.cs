namespace ToyStore.Domain.Carts;

public sealed class CartRuleException(CartRule rule) : Exception(rule.ToString())
{
    public CartRule Rule { get; } = rule;
}
