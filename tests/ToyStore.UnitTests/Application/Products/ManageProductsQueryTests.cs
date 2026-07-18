using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Products.ManageProducts;
using ToyStore.Domain.Products;

namespace ToyStore.UnitTests.Application.Products;

public sealed class ManageProductsQueryTests
{
    [Fact]
    public void QueryRequiresProductManagementPolicyAndValidatorUsesThaiMessages()
    {
        var query = new ManageProductsQuery(
            Search: new string('ก', 201),
            Status: (ProductManagementStatus)99,
            ProductCategoryId: Guid.Empty,
            BrandId: Guid.Empty,
            UniverseId: Guid.Empty,
            Page: 0,
            PageSize: 101);
        var failures = new ManageProductsValidator().Validate(query).Errors;

        Assert.Equal(PolicyNames.CanManageProducts, query.RequiredPolicy);
        Assert.Contains(failures, failure => failure.ErrorMessage.Contains("คำค้นหา", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.ErrorMessage.Contains("สถานะสินค้า", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.ErrorMessage.Contains("หน้า", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.ErrorMessage.Contains("รายการต่อหน้า", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.ErrorMessage.Contains("หมวดหมู่", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.ErrorMessage.Contains("แบรนด์", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.ErrorMessage.Contains("จักรวาล", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandlerPassesAllCatalogFiltersToReader()
    {
        var categoryId = Guid.NewGuid();
        var brandId = Guid.NewGuid();
        var universeId = Guid.NewGuid();
        var reader = new CapturingReader();
        var handler = new ManageProductsHandler(reader);

        var result = await handler.Handle(new ManageProductsQuery(
            Search: "  สินค้า  ",
            Status: ProductManagementStatus.Published,
            ProductCategoryId: categoryId,
            BrandId: brandId,
            UniverseId: universeId,
            Page: 3,
            PageSize: 15), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(reader.Request);
        Assert.Equal(ProductStatus.Published, reader.Request.Status);
        Assert.Equal(categoryId, reader.Request.ProductCategoryId);
        Assert.Equal(brandId, reader.Request.BrandId);
        Assert.Equal(universeId, reader.Request.UniverseId);
        Assert.Equal(3, reader.Request.PageNumber);
        Assert.Equal(15, reader.Request.PageSize);
    }

    private sealed class CapturingReader : IProductManagementReader
    {
        public ProductManagementReadRequest? Request { get; private set; }
        public Task<ProductManagementReadPage> ReadAsync(ProductManagementReadRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new ProductManagementReadPage(
                [], [], [], [], [], [], request.PageNumber, 0));
        }
    }
}
