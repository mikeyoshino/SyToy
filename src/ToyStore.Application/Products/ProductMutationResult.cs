using ToyStore.Domain.Products;

namespace ToyStore.Application.Products;

public sealed record ProductImageMutationResult(
    Guid Id,
    string PublicRelativeUrl,
    string AltText,
    int SortOrder,
    bool IsPrimary);

public sealed record ProductMutationResult(
    Guid Id,
    string DisplayName,
    string EnglishName,
    string Description,
    string Slug,
    Guid ProductCategoryId,
    Guid BrandId,
    Guid UniverseId,
    decimal Price,
    ProductStatus Status,
    long Version,
    IReadOnlyList<ProductImageMutationResult> Images,
    IReadOnlyList<Guid> CharacterIds)
{
    public SaleType SaleType { get; init; }
    public decimal? FullPrice { get; init; }
    public decimal? DepositAmount { get; init; }
    public DateTimeOffset? CloseAtUtc { get; init; }
    public int? EstimatedArrivalMonth { get; init; }
    public int? EstimatedArrivalYear { get; init; }
    public int? TotalCapacity { get; init; }
    public int? MaxPerCustomer { get; init; }
    public int? BalancePaymentDays { get; init; }
    public string? ModelScale { get; init; }

    public static ProductMutationResult From(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);
        return new ProductMutationResult(
            product.Id,
            product.DisplayName,
            product.EnglishName,
            product.Description,
            product.Slug,
            product.ProductCategoryId,
            product.BrandId,
            product.UniverseId,
            product.InStockOffer?.Price.Amount ?? product.PreOrderOffer?.DepositAmount.Amount
                ?? throw new InvalidOperationException("A Product result requires a matching offer."),
            product.Status,
            product.Version,
            product.Images.OrderBy(image => image.SortOrder)
                .Select(image => new ProductImageMutationResult(
                    image.Id,
                    image.PublicRelativeUrl,
                    image.AltText,
                    image.SortOrder,
                    image.IsPrimary))
                .ToArray(),
            product.Characters.Select(link => link.CharacterId).Order().ToArray())
        {
            SaleType = product.SaleType,
            FullPrice = product.PreOrderOffer?.FullPrice.Amount,
            DepositAmount = product.PreOrderOffer?.DepositAmount.Amount,
            CloseAtUtc = product.PreOrderOffer?.CloseAtUtc,
            EstimatedArrivalMonth = product.PreOrderOffer?.EstimatedArrival.Month,
            EstimatedArrivalYear = product.PreOrderOffer?.EstimatedArrival.Year,
            TotalCapacity = product.PreOrderOffer?.TotalCapacity,
            MaxPerCustomer = product.PreOrderOffer?.MaxPerCustomer,
            BalancePaymentDays = product.PreOrderOffer?.BalancePaymentDays,
            ModelScale = product.ModelScale,
        };
    }

    public static ProductMutationResult From(ProductMutationEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return new ProductMutationResult(
            evidence.Id,
            evidence.DisplayName,
            evidence.EnglishName,
            evidence.Description,
            evidence.Slug,
            evidence.ProductCategoryId,
            evidence.BrandId,
            evidence.UniverseId,
            evidence.InStockPrice ?? evidence.PreOrderDepositAmount
                ?? throw new InvalidOperationException("A Product evidence result requires a matching offer."),
            evidence.Status,
            evidence.IntendedVersion,
            evidence.Images.OrderBy(image => image.SortOrder)
                .Select(image => new ProductImageMutationResult(
                    image.Id,
                    image.PublicRelativeUrl,
                    image.AltText,
                    image.SortOrder,
                    image.IsPrimary))
                .ToArray(),
            evidence.CharacterIds.Order().ToArray())
        {
            SaleType = evidence.SaleType,
            FullPrice = evidence.PreOrderFullPrice,
            DepositAmount = evidence.PreOrderDepositAmount,
            CloseAtUtc = evidence.PreOrderCloseAtUtc,
            EstimatedArrivalMonth = evidence.PreOrderEstimatedArrivalMonth,
            EstimatedArrivalYear = evidence.PreOrderEstimatedArrivalYear,
            TotalCapacity = evidence.PreOrderTotalCapacity,
            MaxPerCustomer = evidence.PreOrderMaxPerCustomer,
            BalancePaymentDays = evidence.PreOrderBalancePaymentDays,
            ModelScale = evidence.ModelScale,
        };
    }
}
