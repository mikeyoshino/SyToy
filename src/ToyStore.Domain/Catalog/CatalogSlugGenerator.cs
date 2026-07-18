using System.Text;

namespace ToyStore.Domain.Catalog;

public static class CatalogSlugGenerator
{
    public static CatalogSlug GenerateBase(string englishName)
    {
        if (string.IsNullOrWhiteSpace(englishName))
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.SlugCannotBeGenerated);
        }

        var compatible = englishName.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var builder = new StringBuilder(compatible.Length);
        var separatorPending = false;

        foreach (var character in compatible)
        {
            var isAsciiLetter = character is >= 'a' and <= 'z';
            var isAsciiDigit = character is >= '0' and <= '9';
            if (isAsciiLetter || isAsciiDigit)
            {
                if (separatorPending && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(character);
                separatorPending = false;
            }
            else if (builder.Length > 0)
            {
                separatorPending = true;
            }
        }

        if (builder.Length == 0)
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.SlugCannotBeGenerated);
        }

        return CatalogSlug.Create(builder.ToString());
    }
}
