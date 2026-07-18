using System.Text;

namespace ToyStore.Domain.Catalog;

public static class CatalogNameNormalizer
{
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.NameRequired);
        }

        var compatible = value.Normalize(NormalizationForm.FormKC);
        var normalized = new StringBuilder(compatible.Length);
        var hasPendingSpace = false;

        foreach (var character in compatible)
        {
            if (char.IsWhiteSpace(character))
            {
                hasPendingSpace = normalized.Length > 0;
                continue;
            }

            if (hasPendingSpace)
            {
                normalized.Append(' ');
                hasPendingSpace = false;
            }

            normalized.Append(character);
        }

        return normalized.ToString().ToUpperInvariant();
    }
}
