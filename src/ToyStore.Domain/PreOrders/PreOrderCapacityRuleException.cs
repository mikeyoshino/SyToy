namespace ToyStore.Domain.PreOrders;

public sealed class PreOrderCapacityRuleException(PreOrderCapacityRule rule) : Exception(rule.ToString())
{
    public PreOrderCapacityRule Rule { get; } = rule;
}
