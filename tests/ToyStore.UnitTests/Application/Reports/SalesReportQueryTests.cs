using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Reports;
using ToyStore.Application.Reports.GetSalesReport;

namespace ToyStore.UnitTests.Application.Reports;

public sealed class SalesReportQueryTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 7, 23, 2, 30, 0, TimeSpan.Zero);

    [Fact]
    public void QueryRequiresOrderManagementPermission()
    {
        Assert.Equal(PolicyNames.CanManageOrders, new GetSalesReportQuery().RequiredPolicy);
    }

    [Fact]
    public void ValidatorRequiresCompleteOrderedPeriodNoLongerThanOneYear()
    {
        var validator = new GetSalesReportValidator();

        var missingTo = validator.Validate(new GetSalesReportQuery(new DateOnly(2026, 7, 1)));
        var reversed = validator.Validate(new GetSalesReportQuery(
            new DateOnly(2026, 7, 2), new DateOnly(2026, 7, 1)));
        var tooLong = validator.Validate(new GetSalesReportQuery(
            new DateOnly(2025, 7, 22), new DateOnly(2026, 7, 23)));

        Assert.Contains(missingTo.Errors, error => error.ErrorMessage.Contains("ให้ครบ", StringComparison.Ordinal));
        Assert.Contains(reversed.Errors, error => error.ErrorMessage.Contains("ไม่ก่อน", StringComparison.Ordinal));
        Assert.Contains(tooLong.Errors, error => error.ErrorMessage.Contains("366", StringComparison.Ordinal));
        Assert.True(validator.Validate(new GetSalesReportQuery(
            new DateOnly(2025, 7, 23), new DateOnly(2026, 7, 23))).IsValid);
    }

    [Fact]
    public async Task HandlerUsesBangkokBoundariesAndDefaultsToLastThirtyDays()
    {
        var reader = new CapturingReader();
        var handler = new GetSalesReportHandler(reader, new FixedTimeProvider(FixedNow));

        var result = await handler.Handle(new GetSalesReportQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var request = Assert.IsType<SalesReportReadRequest>(reader.Request);
        Assert.Equal(new DateOnly(2026, 6, 24), request.SelectedFrom);
        Assert.Equal(new DateOnly(2026, 7, 23), request.SelectedTo);
        Assert.Equal(new DateTimeOffset(2026, 6, 23, 17, 0, 0, TimeSpan.Zero), request.SelectedFromUtc);
        Assert.Equal(new DateTimeOffset(2026, 7, 23, 17, 0, 0, TimeSpan.Zero), request.SelectedBeforeUtc);
        Assert.Equal(new DateTimeOffset(2026, 7, 22, 17, 0, 0, TimeSpan.Zero), request.TodayFromUtc);
        Assert.Equal(new DateTimeOffset(2026, 6, 30, 17, 0, 0, TimeSpan.Zero), request.MonthFromUtc);
        Assert.Equal(new DateTimeOffset(2025, 12, 31, 17, 0, 0, TimeSpan.Zero), request.YearFromUtc);
        Assert.Equal(FixedNow, request.SummaryBeforeUtc);
        Assert.Equal(30, result.Value.Trend.Count);
        Assert.All(result.Value.Trend, point => Assert.Equal(0, point.NetSales));
        Assert.Equal(FixedNow, result.Value.GeneratedAtUtc);
    }

    [Fact]
    public async Task HandlerPreservesDatabaseTrendAndFillsMissingDatesWithZero()
    {
        var from = new DateOnly(2026, 7, 20);
        var to = new DateOnly(2026, 7, 22);
        var reader = new CapturingReader([
            new SalesTrendPointView(new DateOnly(2026, 7, 21), 500, 100, 400),
        ]);
        var handler = new GetSalesReportHandler(reader, new FixedTimeProvider(FixedNow));

        var result = await handler.Handle(new GetSalesReportQuery(from, to), CancellationToken.None);

        Assert.Equal([from, new DateOnly(2026, 7, 21), to], result.Value.Trend.Select(x => x.Date));
        Assert.Equal([0m, 400m, 0m], result.Value.Trend.Select(x => x.NetSales));
    }

    private sealed class CapturingReader(IReadOnlyList<SalesTrendPointView>? trend = null)
        : ISalesReportReader
    {
        public SalesReportReadRequest? Request { get; private set; }

        public Task<SalesReportReadResult> ReadAsync(
            SalesReportReadRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new SalesReportReadResult(
                new SalesSummaryView(0, 0, 0, 0),
                new SalesBreakdownView(0, 0, 0, 0, 0, 0, 0, 0),
                trend ?? [], [], [], []));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
