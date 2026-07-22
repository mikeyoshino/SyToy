namespace ToyStore.Application.Reports;

public interface ISalesReportReader
{
    Task<SalesReportReadResult> ReadAsync(
        SalesReportReadRequest request,
        CancellationToken cancellationToken);
}

public sealed record SalesReportReadRequest(
    DateOnly SelectedFrom,
    DateOnly SelectedTo,
    DateTimeOffset SelectedFromUtc,
    DateTimeOffset SelectedBeforeUtc,
    DateTimeOffset TodayFromUtc,
    DateTimeOffset MonthFromUtc,
    DateTimeOffset YearFromUtc,
    DateTimeOffset SummaryBeforeUtc);

public sealed record SalesReportReadResult(
    SalesSummaryView Summary,
    SalesBreakdownView Breakdown,
    IReadOnlyList<SalesTrendPointView> Trend,
    IReadOnlyList<TopProductSalesView> TopProducts,
    IReadOnlyList<TopBrandSalesView> TopBrands,
    IReadOnlyList<RecentPaidOrderView> RecentOrders);

public sealed record SalesSummaryView(
    decimal NetSalesToday,
    decimal NetSalesCurrentMonth,
    decimal NetSalesCurrentYear,
    decimal OutstandingPreOrderBalance);

public sealed record SalesBreakdownView(
    decimal GrossReceived,
    decimal Refunds,
    decimal NetSales,
    decimal InStockFullPayments,
    decimal PreOrderDeposits,
    decimal PreOrderBalancePayments,
    int OrderCount,
    decimal AverageNetOrderValue);

public sealed record SalesTrendPointView(
    DateOnly Date,
    decimal GrossReceived,
    decimal Refunds,
    decimal NetSales);

public sealed record TopProductSalesView(
    Guid ProductId,
    string ProductName,
    string BrandName,
    int Quantity,
    decimal NetSales);

public sealed record TopBrandSalesView(
    string BrandName,
    int Quantity,
    decimal NetSales);

public enum ReportOrderSaleType { InStock, PreOrder }

public sealed record RecentPaidOrderView(
    string OrderNumber,
    ReportOrderSaleType SaleType,
    decimal AmountReceived,
    DateTimeOffset PaidAtUtc);

public sealed record SalesReportView(
    DateOnly SelectedFrom,
    DateOnly SelectedTo,
    DateTimeOffset GeneratedAtUtc,
    SalesSummaryView Summary,
    SalesBreakdownView Breakdown,
    IReadOnlyList<SalesTrendPointView> Trend,
    IReadOnlyList<TopProductSalesView> TopProducts,
    IReadOnlyList<TopBrandSalesView> TopBrands,
    IReadOnlyList<RecentPaidOrderView> RecentOrders);
