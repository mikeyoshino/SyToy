using System.Text.RegularExpressions;

namespace ToyStore.Domain.Catalog;

public readonly partial record struct CatalogSlug
{
    private CatalogSlug(string value) => Value = value;

    public string Value { get; }

    public static CatalogSlug Create(string value)
    {
        if (!IsValid(value))
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.SlugInvalid);
        }

        return new CatalogSlug(value);
    }

    public static bool IsValid(string? value) =>
        value is not null && SlugPattern().IsMatch(value);

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
