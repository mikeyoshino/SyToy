using ToyStore.Application.Common.Models;

namespace ToyStore.UnitTests.Application;

public sealed class PagedResultTests
{
    [Fact]
    public void ConstructorCalculatesPageMetadata()
    {
        var result = new PagedResult<int>([1, 2], 2, 2, 5);

        Assert.Equal([1, 2], result.Items);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.True(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
    }

    [Fact]
    public void EmptyResultHasNoPagesOrNavigation()
    {
        var result = new PagedResult<int>([], 1, 10, 0);

        Assert.Equal(0, result.TotalPages);
        Assert.False(result.HasPreviousPage);
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public void ConstructorRejectsNullItems()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PagedResult<int>(null!, 1, 10, 0));
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(-1, 10, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(1, 10, -1)]
    public void ConstructorRejectsInvalidNumbers(int pageNumber, int pageSize, int totalCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PagedResult<int>([], pageNumber, pageSize, totalCount));
    }

    [Fact]
    public void ConstructorRejectsMoreItemsThanPageSize()
    {
        Assert.Throws<ArgumentException>(
            () => new PagedResult<int>([1, 2, 3], 1, 2, 3));
    }

    [Fact]
    public void ConstructorRejectsMoreItemsThanTotalCount()
    {
        Assert.Throws<ArgumentException>(
            () => new PagedResult<int>([1], 1, 10, 0));
    }

    [Fact]
    public void ConstructorSnapshotsCallerOwnedItems()
    {
        var items = new List<int> { 1 };
        var result = new PagedResult<int>(items, 1, 10, 2);

        items.Add(2);

        Assert.Equal([1], result.Items);
    }
}
