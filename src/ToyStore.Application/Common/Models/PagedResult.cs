namespace ToyStore.Application.Common.Models;

public sealed class PagedResult<T>
{
    public PagedResult(
        IReadOnlyList<T> items,
        int pageNumber,
        int pageSize,
        int totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);

        if (items.Count > pageSize)
        {
            throw new ArgumentException("Item count cannot exceed the page size.", nameof(items));
        }

        if (items.Count > totalCount)
        {
            throw new ArgumentException("Item count cannot exceed the total count.", nameof(items));
        }

        Items = items.ToArray();
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;
        TotalPages = totalCount == 0
            ? 0
            : (totalCount / pageSize) + (totalCount % pageSize == 0 ? 0 : 1);
    }

    public IReadOnlyList<T> Items { get; }

    public int PageNumber { get; }

    public int PageSize { get; }

    public int TotalCount { get; }

    public int TotalPages { get; }

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;
}
