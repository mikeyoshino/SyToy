using System.Globalization;
using ToyStore.Application.Storefront.Catalog;

namespace ToyStore.Web.Components.Storefront.Models;

public sealed record ProductCardModel(
    Guid ProductId,
    string Name,
    string Brand,
    string PriceLabel,
    string TypeLabel,
    string? ImageUrl,
    string ProductUrl,
    StorefrontSaleType SaleType,
    StorefrontOfferState OfferState,
    string? ModelScale = null)
{
    public static ProductCardModel From(StorefrontProductCard item, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(culture);
        var isPreOrder = item.SaleType == StorefrontSaleType.PreOrder;
        var price = isPreOrder
            ? $"มัดจำ ฿{item.DepositAmount!.Value.ToString("N2", culture)}"
            : $"฿{item.Price.ToString("N2", culture)}";
        return new ProductCardModel(
            item.Id,
            item.DisplayName,
            $"{item.BrandName} · {item.CategoryName}",
            price,
            isPreOrder ? "พรีออเดอร์" : "สินค้าพร้อมส่ง",
            item.PrimaryImageUrl,
            $"/products/{item.Slug}",
            item.SaleType,
            item.OfferState,
            item.ModelScale);
    }
}
