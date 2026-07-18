namespace ToyStore.Domain.Catalog;

public sealed class ProductCategory
{
    private ProductCategory()
    {
        Code = null!;
    }

    private ProductCategory(Guid id, string code)
    {
        Id = id;
        Code = code;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; }

    internal static ProductCategory Create(Guid id, string code)
    {
        if (id == Guid.Empty)
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.IdentityRequired);
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.NameRequired);
        }

        return new ProductCategory(id, code);
    }
}
