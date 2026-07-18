using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;

namespace ToyStore.Application.Products.ManageProducts;

public sealed class ManageProductsHandler(IProductManagementReader reader)
    : IRequestHandler<ManageProductsQuery, Result<ProductManagementPage>>
{
    public async Task<Result<ProductManagementPage>> Handle(
        ManageProductsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var read = await reader.ReadAsync(
            new ProductManagementReadRequest(
                string.IsNullOrWhiteSpace(request.Search)
                    ? null
                    : CatalogNameNormalizer.Normalize(request.Search),
                MapStatus(request.Status),
                request.ProductCategoryId,
                request.BrandId,
                request.UniverseId,
                request.Page,
                request.PageSize),
            cancellationToken);
        var items = read.Items.Select(item => new ProductManagementItem(
            item.Id,
            item.DisplayName,
            item.EnglishName,
            item.Description,
            item.Slug,
            item.ProductCategoryId,
            item.ProductCategoryCode,
            item.BrandId,
            item.BrandName,
            item.UniverseId,
            item.UniverseName,
            item.Price,
            MapStatus(item.Status),
            item.Version,
            item.OnHandQuantity,
            item.ReservableQuantity,
            item.Images,
            item.Characters,
            item.UpdatedAtUtc)
        {
            SaleType = item.SaleType,
            FullPrice = item.FullPrice,
            DepositAmount = item.DepositAmount,
            CloseAtUtc = item.CloseAtUtc,
            EstimatedArrivalMonth = item.EstimatedArrivalMonth,
            EstimatedArrivalYear = item.EstimatedArrivalYear,
            TotalCapacity = item.TotalCapacity,
            MaxPerCustomer = item.MaxPerCustomer,
            BalancePaymentDays = item.BalancePaymentDays,
        }).ToArray();
        return Result<ProductManagementPage>.Success(new ProductManagementPage(
            items,
            read.Categories,
            read.BrandFilterOptions,
            read.UniverseFilterOptions,
            read.BrandEditorOptions,
            read.UniverseEditorOptions,
            read.EffectivePageNumber,
            request.PageSize,
            read.TotalCount));
    }

    private static ProductStatus? MapStatus(ProductManagementStatus? status) => status switch
    {
        ProductManagementStatus.Draft => ProductStatus.Draft,
        ProductManagementStatus.Published => ProductStatus.Published,
        ProductManagementStatus.Archived => ProductStatus.Archived,
        null => null,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private static ProductManagementStatus MapStatus(ProductStatus status) => status switch
    {
        ProductStatus.Draft => ProductManagementStatus.Draft,
        ProductStatus.Published => ProductManagementStatus.Published,
        ProductStatus.Archived => ProductManagementStatus.Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}
