namespace ToyStore.Domain.Inventory;

public sealed class InventoryRuleException(InventoryRule rule) : Exception(rule.ToString())
{
    public InventoryRule Rule { get; } = rule;
}
