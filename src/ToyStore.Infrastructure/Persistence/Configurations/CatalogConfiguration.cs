using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal static class CatalogConfiguration
{
    internal const int NameLength = CatalogReferenceLimits.NameLength;
    internal const int SlugLength = CatalogReferenceLimits.SlugLength;
    internal const int ActorLength = CatalogReferenceLimits.ActorLength;
    internal const int StorageKeyLength = CatalogReferenceLimits.StorageKeyLength;
    internal const int UrlLength = CatalogReferenceLimits.PublicRelativeUrlLength;
    internal const int AltTextLength = CatalogReferenceLimits.AltTextLength;

    internal static readonly ValueConverter<CatalogSlug, string> SlugConverter =
        new(slug => slug.Value, value => CatalogSlug.Create(value));

    internal static readonly ValueConverter<Money, decimal> MoneyConverter =
        new(money => money.Amount, value => Money.Create(value));
}
