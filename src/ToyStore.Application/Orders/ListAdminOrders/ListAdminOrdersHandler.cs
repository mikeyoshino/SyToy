using MediatR;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Orders.ListAdminOrders;

public sealed class ListAdminOrdersHandler(IAdminOrderReader reader)
    : IRequestHandler<ListAdminOrdersQuery, Result<AdminOrderPage>>
{
    private static readonly TimeZoneInfo BangkokTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");

    public async Task<Result<AdminOrderPage>> Handle(
        ListAdminOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var page = await reader.ListAsync(new AdminOrderReadRequest(
            NormalizeSearch(request.Search),
            request.SaleType,
            request.PaymentStatus,
            request.FulfillmentStatus,
            request.CreatedFrom is null ? null : BangkokStartUtc(request.CreatedFrom.Value),
            request.CreatedTo is null ? null : BangkokStartUtc(request.CreatedTo.Value.AddDays(1)),
            request.Page,
            request.PageSize), cancellationToken);
        return Result<AdminOrderPage>.Success(page);
    }

    private static string? NormalizeSearch(string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static DateTimeOffset BangkokStartUtc(DateOnly date)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, BangkokTimeZone), TimeSpan.Zero);
    }
}
