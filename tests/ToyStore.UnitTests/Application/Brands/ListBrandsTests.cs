using ToyStore.Application.Brands;
using ToyStore.Application.Brands.ListBrands;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Catalog;

namespace ToyStore.UnitTests.Application.Brands;

public sealed class ListBrandsTests
{
    [Fact]
    public void QueryDefaultsToActiveFirstPageAndProductManagementPolicy()
    {
        var query = new ListBrandsQuery();

        Assert.Null(query.Search);
        Assert.Equal(CatalogReferenceListStatus.Active, query.Status);
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
        Assert.Equal(PolicyNames.CanManageProducts, query.RequiredPolicy);
    }

    [Fact]
    public async Task ValidationBehaviorReturnsStructuredThaiFailuresWithoutCallingHandler()
    {
        var query = new ListBrandsQuery(
            new string('ก', 201),
            (CatalogReferenceListStatus)999,
            Page: 0,
            PageSize: 101);
        var behavior = new ValidationBehavior<
            ListBrandsQuery,
            Result<PagedResult<BrandListItem>>>([new ListBrandsValidator()]);
        var handlerCalled = false;

        var result = await behavior.Handle(
            query,
            _ =>
            {
                handlerCalled = true;
                return Task.FromResult(Result<PagedResult<BrandListItem>>.Success(
                    new PagedResult<BrandListItem>([], 1, 20, 0)));
            },
            TestContext.Current.CancellationToken);

        Assert.False(handlerCalled);
        Assert.Equal("Validation.Failed", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Collection(
            result.ValidationFailures.OrderBy(failure => failure.PropertyName),
            failure =>
            {
                Assert.Equal(nameof(ListBrandsQuery.Page), failure.PropertyName);
                Assert.Equal("หน้าต้องมีค่าตั้งแต่ 1 ขึ้นไป", failure.ErrorMessage);
            },
            failure =>
            {
                Assert.Equal(nameof(ListBrandsQuery.PageSize), failure.PropertyName);
                Assert.Equal("จำนวนรายการต่อหน้าต้องอยู่ระหว่าง 1–100", failure.ErrorMessage);
            },
            failure =>
            {
                Assert.Equal(nameof(ListBrandsQuery.Search), failure.PropertyName);
                Assert.Equal("คำค้นหาต้องไม่เกิน 200 ตัวอักษร", failure.ErrorMessage);
            },
            failure =>
            {
                Assert.Equal(nameof(ListBrandsQuery.Status), failure.PropertyName);
                Assert.Equal("สถานะแบรนด์ไม่ถูกต้อง", failure.ErrorMessage);
            });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void ValidatorAcceptsInclusivePageSizeBoundaries(int pageSize)
    {
        var result = new ListBrandsValidator().Validate(
            new ListBrandsQuery(PageSize: pageSize));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task HandlerNormalizesSearchMapsAllFieldsAndUsesEffectiveCanonicalPage()
    {
        var updatedAtUtc = new DateTimeOffset(2026, 7, 17, 5, 0, 0, TimeSpan.Zero);
        var id = Guid.NewGuid();
        var reader = new CapturingBrandListReader(new BrandListReadPage(
            [new BrandListReadItem(
                id,
                "บันได",
                "Bandai",
                "bandai",
                "/media/brands/bandai.webp",
                "โลโก้แบรนด์ บันได",
                CatalogReferenceStatus.Active,
                CanBeUsedByPublishedProduct: true,
                Version: 7,
                ProductReferenceCount: 12,
                updatedAtUtc)],
            EffectivePageNumber: 3,
            TotalCount: 21));
        var handler = new ListBrandsHandler(reader);

        var result = await handler.Handle(
            new ListBrandsQuery(
                "  ｂａｎｄａｉ  ",
                CatalogReferenceListStatus.All,
                Page: 99,
                PageSize: 10),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.NotNull(reader.Request);
        Assert.Equal("BANDAI", reader.Request.NormalizedSearch);
        Assert.Null(reader.Request.Status);
        Assert.Equal(99, reader.Request.PageNumber);
        Assert.Equal(10, reader.Request.PageSize);
        Assert.Equal(3, result.Value.PageNumber);
        Assert.Equal(10, result.Value.PageSize);
        Assert.Equal(21, result.Value.TotalCount);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(id, item.Id);
        Assert.Equal("บันได", item.DisplayName);
        Assert.Equal("Bandai", item.EnglishName);
        Assert.Equal("bandai", item.Slug);
        Assert.Equal("/media/brands/bandai.webp", item.ImagePublicRelativeUrl);
        Assert.Equal("โลโก้แบรนด์ บันได", item.ImageAltText);
        Assert.Equal(CatalogReferenceStatus.Active, item.Status);
        Assert.True(item.CanBeUsedByPublishedProduct);
        Assert.Equal(7, item.Version);
        Assert.Equal(12, item.ProductReferenceCount);
        Assert.Equal(updatedAtUtc, item.UpdatedAtUtc);
        Assert.Equal(TestContext.Current.CancellationToken, reader.CancellationToken);
    }

    [Theory]
    [InlineData(CatalogReferenceListStatus.Active, CatalogReferenceStatus.Active)]
    [InlineData(CatalogReferenceListStatus.Archived, CatalogReferenceStatus.Archived)]
    public async Task HandlerMapsLifecycleFilterToReadPort(
        CatalogReferenceListStatus requested,
        CatalogReferenceStatus expected)
    {
        var reader = new CapturingBrandListReader(
            new BrandListReadPage([], EffectivePageNumber: 1, TotalCount: 0));
        var handler = new ListBrandsHandler(reader);

        var result = await handler.Handle(
            new ListBrandsQuery(Status: requested),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.NotNull(reader.Request);
        Assert.Equal(expected, reader.Request.Status);
        Assert.Equal(1, result.Value.PageNumber);
        Assert.Equal(0, result.Value.TotalCount);
        Assert.Equal(0, result.Value.TotalPages);
        Assert.Empty(result.Value.Items);
    }

    private sealed class CapturingBrandListReader(BrandListReadPage response) : IBrandListReader
    {
        public BrandListReadRequest? Request { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<BrandListReadPage> ReadAsync(
            BrandListReadRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            CancellationToken = cancellationToken;
            return Task.FromResult(response);
        }
    }
}
