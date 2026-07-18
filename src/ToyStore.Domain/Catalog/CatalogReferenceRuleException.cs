namespace ToyStore.Domain.Catalog;

public sealed class CatalogReferenceRuleException(CatalogReferenceRule rule) : Exception(rule.ToString())
{
    public CatalogReferenceRule Rule { get; } = rule;
}
