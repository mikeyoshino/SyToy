using Microsoft.EntityFrameworkCore;
using ToyStore.Application.Reports;
using ToyStore.Domain.Orders;
using ToyStore.Domain.Products;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class SalesReportReader(
    IDbContextFactory<ApplicationDbContext> contextFactory)
    : ISalesReportReader
{
    public async Task<SalesReportReadResult> ReadAsync(
        SalesReportReadRequest request,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var today = await NetSalesAsync(
            db, request.TodayFromUtc, request.SummaryBeforeUtc, cancellationToken);
        var month = await NetSalesAsync(
            db, request.MonthFromUtc, request.SummaryBeforeUtc, cancellationToken);
        var year = await NetSalesAsync(
            db, request.YearFromUtc, request.SummaryBeforeUtc, cancellationToken);
        var outstandingBalance = await db.Orders.AsNoTracking()
            .Where(order => order.SaleType == SaleType.PreOrder
                && order.PaymentStatus == PaymentStatus.DepositPaid
                && order.FulfillmentStatus != FulfillmentStatus.Cancelled)
            .SelectMany(order => order.Items)
            .Select(item => (decimal?)(item.BalanceAmount * item.Quantity))
            .SumAsync(cancellationToken) ?? 0m;

        var paymentQuery = from payment in db.Payments.AsNoTracking()
                           join order in db.Orders.AsNoTracking()
                               on payment.OrderId equals order.Id
                           where payment.PaidAtUtc >= request.SelectedFromUtc
                               && payment.PaidAtUtc < request.SelectedBeforeUtc
                           select new { Payment = payment, Order = order };
        var aggregate = await paymentQuery.GroupBy(_ => 1)
            .Select(group => new PeriodAggregate(
                group.Sum(row => row.Payment.Purpose == PaymentPurpose.Refund ? 0m : row.Payment.Amount),
                group.Sum(row => row.Payment.Purpose == PaymentPurpose.Refund ? row.Payment.Amount : 0m),
                group.Sum(row => row.Payment.Purpose == PaymentPurpose.Full
                        && row.Order.SaleType == SaleType.InStock
                    ? row.Payment.Amount : 0m),
                group.Sum(row => row.Payment.Purpose == PaymentPurpose.Deposit
                    ? row.Payment.Amount : 0m),
                group.Sum(row => row.Payment.Purpose == PaymentPurpose.Balance
                    ? row.Payment.Amount : 0m)))
            .SingleOrDefaultAsync(cancellationToken);
        var orderCount = await paymentQuery
            .Where(row => row.Payment.Purpose != PaymentPurpose.Refund)
            .Select(row => row.Payment.OrderId)
            .Distinct()
            .CountAsync(cancellationToken);
        var gross = aggregate?.GrossReceived ?? 0m;
        var refunds = aggregate?.Refunds ?? 0m;
        var net = gross - refunds;
        var breakdown = new SalesBreakdownView(
            gross,
            refunds,
            net,
            aggregate?.InStockFullPayments ?? 0m,
            aggregate?.PreOrderDeposits ?? 0m,
            aggregate?.PreOrderBalancePayments ?? 0m,
            orderCount,
            orderCount == 0 ? 0m : decimal.Round(net / orderCount, 2));

        var trendRows = await db.Database.SqlQuery<TrendSqlRow>($"""
            SELECT
                (timezone('Asia/Bangkok', p."PaidAtUtc"))::date AS "Date",
                SUM(CASE WHEN p."Purpose" <> 'Refund' THEN p."Amount" ELSE 0 END) AS "GrossReceived",
                SUM(CASE WHEN p."Purpose" = 'Refund' THEN p."Amount" ELSE 0 END) AS "Refunds"
            FROM "Payments" AS p
            WHERE p."PaidAtUtc" >= {request.SelectedFromUtc}
              AND p."PaidAtUtc" < {request.SelectedBeforeUtc}
            GROUP BY (timezone('Asia/Bangkok', p."PaidAtUtc"))::date
            ORDER BY "Date"
            """).ToArrayAsync(cancellationToken);
        var topProductRows = await db.Database.SqlQuery<TopProductSqlRow>($"""
            WITH payment_totals AS (
                SELECT
                    p."OrderId",
                    SUM(CASE WHEN p."Purpose" <> 'Refund' THEN p."Amount" ELSE 0 END) AS "GrossReceived",
                    SUM(CASE WHEN p."Purpose" = 'Refund' THEN p."Amount" ELSE 0 END) AS "Refunds"
                FROM "Payments" AS p
                WHERE p."PaidAtUtc" >= {request.SelectedFromUtc}
                  AND p."PaidAtUtc" < {request.SelectedBeforeUtc}
                GROUP BY p."OrderId"
            ), order_weights AS (
                SELECT oi."OrderId", SUM(oi."LinePaidAmount") AS "Weight"
                FROM "OrderItems" AS oi
                GROUP BY oi."OrderId"
            )
            SELECT
                oi."ProductId" AS "ProductId",
                MIN(oi."DisplayName") AS "ProductName",
                MIN(oi."BrandName") AS "BrandName",
                CAST(SUM(CASE WHEN pt."GrossReceived" > 0 THEN oi."Quantity" ELSE 0 END) AS integer) AS "Quantity",
                SUM((pt."GrossReceived" - pt."Refunds") * oi."LinePaidAmount"
                    / NULLIF(ow."Weight", 0)) AS "NetSales"
            FROM payment_totals AS pt
            JOIN order_weights AS ow ON ow."OrderId" = pt."OrderId"
            JOIN "OrderItems" AS oi ON oi."OrderId" = pt."OrderId"
            GROUP BY oi."ProductId"
            ORDER BY "NetSales" DESC, "ProductName", oi."ProductId"
            LIMIT 10
            """).ToArrayAsync(cancellationToken);
        var topBrandRows = await db.Database.SqlQuery<TopBrandSqlRow>($"""
            WITH payment_totals AS (
                SELECT
                    p."OrderId",
                    SUM(CASE WHEN p."Purpose" <> 'Refund' THEN p."Amount" ELSE 0 END) AS "GrossReceived",
                    SUM(CASE WHEN p."Purpose" = 'Refund' THEN p."Amount" ELSE 0 END) AS "Refunds"
                FROM "Payments" AS p
                WHERE p."PaidAtUtc" >= {request.SelectedFromUtc}
                  AND p."PaidAtUtc" < {request.SelectedBeforeUtc}
                GROUP BY p."OrderId"
            ), order_weights AS (
                SELECT oi."OrderId", SUM(oi."LinePaidAmount") AS "Weight"
                FROM "OrderItems" AS oi
                GROUP BY oi."OrderId"
            )
            SELECT
                oi."BrandName" AS "BrandName",
                CAST(SUM(CASE WHEN pt."GrossReceived" > 0 THEN oi."Quantity" ELSE 0 END) AS integer) AS "Quantity",
                SUM((pt."GrossReceived" - pt."Refunds") * oi."LinePaidAmount"
                    / NULLIF(ow."Weight", 0)) AS "NetSales"
            FROM payment_totals AS pt
            JOIN order_weights AS ow ON ow."OrderId" = pt."OrderId"
            JOIN "OrderItems" AS oi ON oi."OrderId" = pt."OrderId"
            GROUP BY oi."BrandName"
            ORDER BY "NetSales" DESC, "BrandName"
            LIMIT 10
            """).ToArrayAsync(cancellationToken);
        var recentRows = await db.Database.SqlQuery<RecentPaidOrderSqlRow>($"""
            SELECT
                o."Number" AS "OrderNumber",
                o."SaleType" AS "SaleType",
                SUM(p."Amount") AS "AmountReceived",
                MAX(p."PaidAtUtc") AS "PaidAtUtc"
            FROM "Payments" AS p
            JOIN "Orders" AS o ON o."Id" = p."OrderId"
            WHERE p."PaidAtUtc" >= {request.SelectedFromUtc}
              AND p."PaidAtUtc" < {request.SelectedBeforeUtc}
              AND p."Purpose" <> 'Refund'
            GROUP BY o."Id", o."Number", o."SaleType"
            ORDER BY "PaidAtUtc" DESC, "OrderNumber" DESC
            LIMIT 10
            """).ToArrayAsync(cancellationToken);

        return new SalesReportReadResult(
            new SalesSummaryView(today, month, year, outstandingBalance),
            breakdown,
            trendRows.Select(row => new SalesTrendPointView(
                row.Date, row.GrossReceived, row.Refunds, row.GrossReceived - row.Refunds)).ToArray(),
            topProductRows.Select(row => new TopProductSalesView(
                row.ProductId, row.ProductName, row.BrandName, row.Quantity, row.NetSales)).ToArray(),
            topBrandRows.Select(row => new TopBrandSalesView(
                row.BrandName, row.Quantity, row.NetSales)).ToArray(),
            recentRows.Select(row => new RecentPaidOrderView(
                row.OrderNumber,
                row.SaleType == "PreOrder" ? ReportOrderSaleType.PreOrder : ReportOrderSaleType.InStock,
                row.AmountReceived,
                row.PaidAtUtc)).ToArray());
    }

    private static async Task<decimal> NetSalesAsync(
        ApplicationDbContext db,
        DateTimeOffset fromUtc,
        DateTimeOffset beforeUtc,
        CancellationToken cancellationToken)
    {
        return await db.Payments.AsNoTracking()
            .Where(payment => payment.PaidAtUtc >= fromUtc && payment.PaidAtUtc < beforeUtc)
            .Select(payment => (decimal?)(payment.Purpose == PaymentPurpose.Refund
                ? -payment.Amount
                : payment.Amount))
            .SumAsync(cancellationToken) ?? 0m;
    }

    private sealed record PeriodAggregate(
        decimal GrossReceived,
        decimal Refunds,
        decimal InStockFullPayments,
        decimal PreOrderDeposits,
        decimal PreOrderBalancePayments);

    private sealed class TrendSqlRow
    {
        public DateOnly Date { get; init; }
        public decimal GrossReceived { get; init; }
        public decimal Refunds { get; init; }
    }

    private sealed class TopProductSqlRow
    {
        public Guid ProductId { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public string BrandName { get; init; } = string.Empty;
        public int Quantity { get; init; }
        public decimal NetSales { get; init; }
    }

    private sealed class TopBrandSqlRow
    {
        public string BrandName { get; init; } = string.Empty;
        public int Quantity { get; init; }
        public decimal NetSales { get; init; }
    }

    private sealed class RecentPaidOrderSqlRow
    {
        public string OrderNumber { get; init; } = string.Empty;
        public string SaleType { get; init; } = string.Empty;
        public decimal AmountReceived { get; init; }
        public DateTimeOffset PaidAtUtc { get; init; }
    }
}
