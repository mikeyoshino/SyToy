using MediatR;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Reports.GetSalesReport;

public sealed class GetSalesReportHandler(
    ISalesReportReader reader,
    TimeProvider timeProvider)
    : IRequestHandler<GetSalesReportQuery, Result<SalesReportView>>
{
    private static readonly TimeZoneInfo BangkokTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");

    public async Task<Result<SalesReportView>> Handle(
        GetSalesReportQuery request,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().ToUniversalTime();
        var bangkokNow = TimeZoneInfo.ConvertTime(nowUtc, BangkokTimeZone);
        var today = DateOnly.FromDateTime(bangkokNow.DateTime);
        var selectedTo = request.To ?? today;
        var selectedFrom = request.From ?? selectedTo.AddDays(-29);
        var monthFrom = new DateOnly(today.Year, today.Month, 1);
        var yearFrom = new DateOnly(today.Year, 1, 1);

        var result = await reader.ReadAsync(new SalesReportReadRequest(
            selectedFrom,
            selectedTo,
            BangkokStartUtc(selectedFrom),
            BangkokStartUtc(selectedTo.AddDays(1)),
            BangkokStartUtc(today),
            BangkokStartUtc(monthFrom),
            BangkokStartUtc(yearFrom),
            nowUtc), cancellationToken);

        var trendByDate = result.Trend.ToDictionary(point => point.Date);
        var completeTrend = new List<SalesTrendPointView>(
            selectedTo.DayNumber - selectedFrom.DayNumber + 1);
        for (var date = selectedFrom; date <= selectedTo; date = date.AddDays(1))
        {
            completeTrend.Add(trendByDate.TryGetValue(date, out var point)
                ? point
                : new SalesTrendPointView(date, 0, 0, 0));
        }

        return Result<SalesReportView>.Success(new SalesReportView(
            selectedFrom,
            selectedTo,
            nowUtc,
            result.Summary,
            result.Breakdown,
            completeTrend,
            result.TopProducts,
            result.TopBrands,
            result.RecentOrders));
    }

    private static DateTimeOffset BangkokStartUtc(DateOnly date)
    {
        var local = DateTime.SpecifyKind(
            date.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);
        return new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(local, BangkokTimeZone),
            TimeSpan.Zero);
    }
}
