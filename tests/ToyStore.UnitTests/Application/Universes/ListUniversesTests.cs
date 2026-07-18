using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Universes;
using ToyStore.Application.Universes.ListUniverses;
using ToyStore.Domain.Catalog;

namespace ToyStore.UnitTests.Application.Universes;

public sealed class ListUniversesTests
{
    [Fact]
    public void QueryDefaultsToActiveFirstPageAndProductManagementPolicy()
    {
        var query = new ListUniversesQuery();

        Assert.Null(query.Search);
        Assert.Equal(CatalogReferenceListStatus.Active, query.Status);
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
        Assert.Equal(PolicyNames.CanManageProducts, query.RequiredPolicy);
    }

    [Fact]
    public async Task ValidationBehaviorReturnsStructuredThaiFailuresWithoutCallingHandler()
    {
        var query = new ListUniversesQuery(
            new string('จ', 201),
            (CatalogReferenceListStatus)999,
            Page: 0,
            PageSize: 101);
        var behavior = new ValidationBehavior<
            ListUniversesQuery,
            Result<PagedResult<UniverseListItem>>>([new ListUniversesValidator()]);
        var handlerCalled = false;

        var result = await behavior.Handle(
            query,
            _ =>
            {
                handlerCalled = true;
                return Task.FromResult(Result<PagedResult<UniverseListItem>>.Success(
                    new PagedResult<UniverseListItem>([], 1, 20, 0)));
            },
            TestContext.Current.CancellationToken);

        Assert.False(handlerCalled);
        Assert.Equal("Validation.Failed", result.Error.Code);
        Assert.Collection(
            result.ValidationFailures.OrderBy(failure => failure.PropertyName),
            failure =>
            {
                Assert.Equal(nameof(ListUniversesQuery.Page), failure.PropertyName);
                Assert.Equal("หน้าต้องมีค่าตั้งแต่ 1 ขึ้นไป", failure.ErrorMessage);
            },
            failure =>
            {
                Assert.Equal(nameof(ListUniversesQuery.PageSize), failure.PropertyName);
                Assert.Equal("จำนวนรายการต่อหน้าต้องอยู่ระหว่าง 1–100", failure.ErrorMessage);
            },
            failure =>
            {
                Assert.Equal(nameof(ListUniversesQuery.Search), failure.PropertyName);
                Assert.Equal("คำค้นหาต้องไม่เกิน 200 ตัวอักษร", failure.ErrorMessage);
            },
            failure =>
            {
                Assert.Equal(nameof(ListUniversesQuery.Status), failure.PropertyName);
                Assert.Equal("สถานะจักรวาลไม่ถูกต้อง", failure.ErrorMessage);
            });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void ValidatorAcceptsInclusivePageSizeBoundaries(int pageSize)
    {
        var result = new ListUniversesValidator().Validate(
            new ListUniversesQuery(PageSize: pageSize));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task HandlerNormalizesSearchMapsBothCountsAndUsesEffectiveCanonicalPage()
    {
        var updatedAtUtc = new DateTimeOffset(2026, 7, 17, 5, 0, 0, TimeSpan.Zero);
        var id = Guid.NewGuid();
        var reader = new CapturingUniverseListReader(new UniverseListReadPage(
            [new UniverseListReadItem(
                id,
                "มาร์เวล",
                "Marvel",
                "marvel",
                "/media/universes/marvel.webp",
                "โลโก้จักรวาล มาร์เวล",
                CatalogReferenceStatus.Active,
                CanBeUsedByPublishedProduct: true,
                Version: 4,
                ProductReferenceCount: 9,
                CharacterReferenceCount: 17,
                updatedAtUtc)],
            EffectivePageNumber: 2,
            TotalCount: 11));
        var handler = new ListUniversesHandler(reader);

        var result = await handler.Handle(
            new ListUniversesQuery(
                "  Ｍａｒｖｅｌ  ",
                CatalogReferenceListStatus.All,
                Page: 99,
                PageSize: 10),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.NotNull(reader.Request);
        Assert.Equal("MARVEL", reader.Request.NormalizedSearch);
        Assert.Null(reader.Request.Status);
        Assert.Equal(99, reader.Request.PageNumber);
        Assert.Equal(10, reader.Request.PageSize);
        Assert.Equal(2, result.Value.PageNumber);
        Assert.Equal(11, result.Value.TotalCount);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(id, item.Id);
        Assert.Equal("มาร์เวล", item.DisplayName);
        Assert.Equal("Marvel", item.EnglishName);
        Assert.Equal("marvel", item.Slug);
        Assert.Equal("/media/universes/marvel.webp", item.LogoPublicRelativeUrl);
        Assert.Equal("โลโก้จักรวาล มาร์เวล", item.LogoAltText);
        Assert.Equal(CatalogReferenceStatus.Active, item.Status);
        Assert.True(item.CanBeUsedByPublishedProduct);
        Assert.Equal(4, item.Version);
        Assert.Equal(9, item.ProductReferenceCount);
        Assert.Equal(17, item.CharacterReferenceCount);
        Assert.Equal(updatedAtUtc, item.UpdatedAtUtc);
        Assert.Equal(TestContext.Current.CancellationToken, reader.CancellationToken);
    }

    [Theory]
    [InlineData(CatalogReferenceListStatus.Active, CatalogReferenceStatus.Active)]
    [InlineData(CatalogReferenceListStatus.Archived, CatalogReferenceStatus.Archived)]
    public async Task HandlerMapsLifecycleFilterToDistinctUniverseReadPort(
        CatalogReferenceListStatus requested,
        CatalogReferenceStatus expected)
    {
        var reader = new CapturingUniverseListReader(
            new UniverseListReadPage([], EffectivePageNumber: 1, TotalCount: 0));
        var result = await new ListUniversesHandler(reader).Handle(
            new ListUniversesQuery(Status: requested),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.NotNull(reader.Request);
        Assert.Equal(expected, reader.Request.Status);
        Assert.Equal(1, result.Value.PageNumber);
        Assert.Equal(0, result.Value.TotalPages);
        Assert.Empty(result.Value.Items);
    }

    private sealed class CapturingUniverseListReader(UniverseListReadPage response)
        : IUniverseListReader
    {
        public UniverseListReadRequest? Request { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<UniverseListReadPage> ReadAsync(
            UniverseListReadRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            CancellationToken = cancellationToken;
            return Task.FromResult(response);
        }
    }
}
