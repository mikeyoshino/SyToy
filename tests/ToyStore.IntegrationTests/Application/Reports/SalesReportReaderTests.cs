using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ToyStore.Application.Checkout;
using ToyStore.Application.Reports;
using ToyStore.Domain.Carts;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Checkouts;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.Orders;
using ToyStore.Domain.PreOrders;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Identity;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Application.Reports;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class SalesReportReaderTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task ReaderAggregatesVerifiedPaymentsByBangkokBusinessDate()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        _ = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        var paidAtUtc = DateTimeOffset.UtcNow.AddMinutes(1);
        var bangkok = TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");
        var businessDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(paidAtUtc, bangkok).DateTime);
        var selectedFromUtc = BangkokStartUtc(businessDate, bangkok);
        var selectedBeforeUtc = BangkokStartUtc(businessDate.AddDays(1), bangkok);
        var monthFrom = new DateOnly(businessDate.Year, businessDate.Month, 1);
        var yearFrom = new DateOnly(businessDate.Year, 1, 1);
        var seeded = await SeedAsync(factory, paidAtUtc.AddMinutes(-10));

        await using (var checkoutScope = factory.Services.CreateAsyncScope())
        {
            var store = checkoutScope.ServiceProvider.GetRequiredService<IInStockCheckoutStore>();
            var checkoutId = Guid.NewGuid();
            var prepared = await store.PrepareAsync(new(
                checkoutId,
                seeded.CustomerId,
                ShippingAddressSnapshot.Create(
                    "ผู้รับสินค้า", "0812345678", "99 ถนนสุขุมวิท",
                    "คลองเตย", "คลองเตย", "กรุงเทพมหานคร", "10110"),
                Guid.NewGuid().ToString("N"),
                paidAtUtc.AddMinutes(-5)), TestContext.Current.CancellationToken);
            Assert.True(prepared.IsSuccess);
            Assert.True((await store.AttachProviderSessionAsync(
                seeded.CustomerId, checkoutId, "cs_report",
                TestContext.Current.CancellationToken)).IsSuccess);
            var fulfilled = await store.FulfillAsync(new PaymentWebhookEvidence(
                "evt_report", "checkout.session.completed", "cs_report", checkoutId,
                "pi_report", 200000, "thb", true, paidAtUtc, "instock_full"),
                TestContext.Current.CancellationToken);
            Assert.True(fulfilled.IsSuccess);
        }

        await using var reportScope = factory.Services.CreateAsyncScope();
        var reader = reportScope.ServiceProvider.GetRequiredService<ISalesReportReader>();
        var readRequest = new SalesReportReadRequest(
            businessDate,
            businessDate,
            selectedFromUtc,
            selectedBeforeUtc,
            selectedFromUtc,
            BangkokStartUtc(monthFrom, bangkok),
            BangkokStartUtc(yearFrom, bangkok),
            paidAtUtc.AddSeconds(1));
        var result = await reader.ReadAsync(readRequest,
            TestContext.Current.CancellationToken);

        Assert.Equal(2000m, result.Summary.NetSalesToday);
        Assert.Equal(2000m, result.Summary.NetSalesCurrentMonth);
        Assert.Equal(2000m, result.Summary.NetSalesCurrentYear);
        Assert.Equal(0m, result.Summary.OutstandingPreOrderBalance);
        Assert.Equal(2000m, result.Breakdown.GrossReceived);
        Assert.Equal(0m, result.Breakdown.Refunds);
        Assert.Equal(2000m, result.Breakdown.NetSales);
        Assert.Equal(2000m, result.Breakdown.InStockFullPayments);
        Assert.Equal(1, result.Breakdown.OrderCount);
        Assert.Equal(2000m, result.Breakdown.AverageNetOrderValue);
        var trend = Assert.Single(result.Trend);
        Assert.Equal(businessDate, trend.Date);
        Assert.Equal(2000m, trend.NetSales);
        var product = Assert.Single(result.TopProducts);
        Assert.Equal(seeded.ProductId, product.ProductId);
        Assert.Equal("สินค้า Report", product.ProductName);
        Assert.Equal("แบรนด์ Report", product.BrandName);
        Assert.Equal(2, product.Quantity);
        Assert.Equal(2000m, product.NetSales);
        var brand = Assert.Single(result.TopBrands);
        Assert.Equal("แบรนด์ Report", brand.BrandName);
        Assert.Equal(2, brand.Quantity);
        Assert.Equal(2000m, brand.NetSales);
        var recent = Assert.Single(result.RecentOrders);
        Assert.Equal(ReportOrderSaleType.InStock, recent.SaleType);
        Assert.Equal(2000m, recent.AmountReceived);
        Assert.Equal(paidAtUtc, recent.PaidAtUtc);

        await using (var refundScope = factory.Services.CreateAsyncScope())
        {
            var db = refundScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Payments.ExecuteUpdateAsync(
                setters => setters.SetProperty(payment => payment.Purpose, PaymentPurpose.Refund),
                TestContext.Current.CancellationToken);
        }

        var refunded = await reader.ReadAsync(readRequest, TestContext.Current.CancellationToken);
        Assert.Equal(-2000m, refunded.Summary.NetSalesToday);
        Assert.Equal(0m, refunded.Breakdown.GrossReceived);
        Assert.Equal(2000m, refunded.Breakdown.Refunds);
        Assert.Equal(-2000m, refunded.Breakdown.NetSales);
        Assert.Equal(0, refunded.Breakdown.OrderCount);
        Assert.Equal(0m, refunded.Breakdown.AverageNetOrderValue);
        Assert.Equal(-2000m, Assert.Single(refunded.Trend).NetSales);
        Assert.Empty(refunded.RecentOrders);
    }

    [Fact]
    public async Task ReaderSeparatesPreOrderDepositFromOutstandingBalance()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        _ = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        var paidAtUtc = DateTimeOffset.UtcNow.AddMinutes(1);
        var bangkok = TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");
        var businessDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(paidAtUtc, bangkok).DateTime);
        var seeded = await SeedPreOrderAsync(factory, paidAtUtc.AddMinutes(-10), businessDate);

        await using (var checkoutScope = factory.Services.CreateAsyncScope())
        {
            var store = checkoutScope.ServiceProvider.GetRequiredService<IPreOrderCheckoutStore>();
            var checkoutId = Guid.NewGuid();
            var prepared = await store.PrepareAsync(new(
                checkoutId, Guid.NewGuid(), Guid.NewGuid(), seeded.ProductId, 2,
                seeded.CustomerId,
                ShippingAddressSnapshot.Create(
                    "ผู้รับพรีออเดอร์", "0812345678", "99 ถนนสุขุมวิท",
                    "คลองเตย", "คลองเตย", "กรุงเทพมหานคร", "10110"),
                Guid.NewGuid().ToString("N"), paidAtUtc.AddMinutes(-5)),
                TestContext.Current.CancellationToken);
            Assert.True(prepared.IsSuccess);
            Assert.Equal(400m, prepared.Value.PaymentAmount);
            Assert.True((await store.AttachProviderSessionAsync(
                seeded.CustomerId, checkoutId, "cs_report_preorder",
                TestContext.Current.CancellationToken)).IsSuccess);
            var fulfilled = await store.FulfillAsync(new PaymentWebhookEvidence(
                "evt_report_preorder", "checkout.session.completed", "cs_report_preorder",
                checkoutId, "pi_report_preorder", 40000, "thb", true,
                paidAtUtc, "preorder_deposit"), TestContext.Current.CancellationToken);
            Assert.True(fulfilled.IsSuccess);
        }

        var fromUtc = BangkokStartUtc(businessDate, bangkok);
        await using var reportScope = factory.Services.CreateAsyncScope();
        var reader = reportScope.ServiceProvider.GetRequiredService<ISalesReportReader>();
        var result = await reader.ReadAsync(new SalesReportReadRequest(
            businessDate, businessDate, fromUtc,
            BangkokStartUtc(businessDate.AddDays(1), bangkok), fromUtc,
            BangkokStartUtc(new DateOnly(businessDate.Year, businessDate.Month, 1), bangkok),
            BangkokStartUtc(new DateOnly(businessDate.Year, 1, 1), bangkok),
            paidAtUtc.AddSeconds(1)), TestContext.Current.CancellationToken);

        Assert.Equal(400m, result.Summary.NetSalesToday);
        Assert.Equal(1600m, result.Summary.OutstandingPreOrderBalance);
        Assert.Equal(400m, result.Breakdown.GrossReceived);
        Assert.Equal(400m, result.Breakdown.PreOrderDeposits);
        Assert.Equal(0m, result.Breakdown.InStockFullPayments);
        Assert.Equal(0m, result.Breakdown.PreOrderBalancePayments);
        Assert.Equal(2, Assert.Single(result.TopProducts).Quantity);
        Assert.Equal(ReportOrderSaleType.PreOrder, Assert.Single(result.RecentOrders).SaleType);

        await using (var balanceScope = factory.Services.CreateAsyncScope())
        {
            var db = balanceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Payments.ExecuteUpdateAsync(
                setters => setters.SetProperty(payment => payment.Purpose, PaymentPurpose.Balance),
                TestContext.Current.CancellationToken);
        }

        var balance = await reader.ReadAsync(new SalesReportReadRequest(
            businessDate, businessDate, fromUtc,
            BangkokStartUtc(businessDate.AddDays(1), bangkok), fromUtc,
            BangkokStartUtc(new DateOnly(businessDate.Year, businessDate.Month, 1), bangkok),
            BangkokStartUtc(new DateOnly(businessDate.Year, 1, 1), bangkok),
            paidAtUtc.AddSeconds(1)), TestContext.Current.CancellationToken);
        Assert.Equal(0m, balance.Breakdown.PreOrderDeposits);
        Assert.Equal(400m, balance.Breakdown.PreOrderBalancePayments);
    }

    private static DateTimeOffset BangkokStartUtc(DateOnly date, TimeZoneInfo bangkok)
    {
        var local = DateTime.SpecifyKind(
            date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, bangkok), TimeSpan.Zero);
    }

    private static async Task<(string CustomerId, Guid ProductId)> SeedAsync(
        ToyStoreWebApplicationFactory factory,
        DateTimeOffset now)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var customerId = Guid.NewGuid().ToString("N");
        var email = $"{customerId}@example.test";
        db.Users.Add(new ApplicationUser
        {
            Id = customerId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
        });
        var brand = Brand.Create(
            Guid.NewGuid(), "แบรนด์ Report", "Report Brand",
            CatalogSlug.Create("report-brand"), now.AddMinutes(-2), "test");
        var product = Product.CreateInStock(
            Guid.NewGuid(), "สินค้า Report", "Report Product", "รายละเอียด", "report-product",
            CatalogSeedIds.ArtToyCategory, brand.Id, CatalogSeedIds.MarvelUniverse,
            InStockOffer.Create(Money.Create(1000)),
            [new ProductImageDefinition(
                Guid.NewGuid(), "report/main.webp", "/media/report.webp", "สินค้า Report")],
            [], now.AddMinutes(-1), "test");
        product.Publish(product.Version, now, "test");
        var inventory = InventoryItem.Create(
            Guid.NewGuid(), product.Id, Guid.NewGuid(), 2,
            "สต็อกเริ่มต้น", "report-test", now, "test");
        var cart = ToyStore.Domain.Carts.Cart.Create(Guid.NewGuid(), customerId, now);
        cart.Add(product, 2, cart.Version, now);
        db.Brands.Add(brand);
        db.Products.Add(product);
        db.InventoryItems.Add(inventory.Item);
        db.StockMovements.Add(inventory.InitialMovement);
        db.Carts.Add(cart);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (customerId, product.Id);
    }

    private static async Task<(string CustomerId, Guid ProductId)> SeedPreOrderAsync(
        ToyStoreWebApplicationFactory factory,
        DateTimeOffset now,
        DateOnly businessDate)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var customerId = Guid.NewGuid().ToString("N");
        var email = $"{customerId}@example.test";
        db.Users.Add(new ApplicationUser
        {
            Id = customerId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
        });
        var brand = Brand.Create(
            Guid.NewGuid(), "แบรนด์ Pre-order Report", "Pre-order Report Brand",
            CatalogSlug.Create("preorder-report-brand"), now.AddMinutes(-2), "test");
        var closeDate = businessDate.AddMonths(3);
        var arrivalDate = closeDate.AddMonths(2);
        var offer = PreOrderOffer.Create(
            Money.Create(1000), Money.Create(200), closeDate,
            EstimatedArrival.Create(arrivalDate.Month, arrivalDate.Year),
            5, 3, now.AddMinutes(-1));
        var product = Product.CreatePreOrder(
            Guid.NewGuid(), "สินค้า Pre-order Report", "Pre-order Report Product",
            "รายละเอียด", "preorder-report-product", CatalogSeedIds.ArtToyCategory,
            brand.Id, CatalogSeedIds.MarvelUniverse, offer,
            [new ProductImageDefinition(
                Guid.NewGuid(), "report/preorder.webp", "/media/preorder-report.webp",
                "สินค้า Pre-order Report")], [], now.AddMinutes(-1), "test");
        product.Publish(product.Version, now, "test");
        var capacity = PreOrderCapacity.Create(
            Guid.NewGuid(), product.Id, Guid.NewGuid(), offer,
            "เปิดรอบ", "report-preorder", now, "test");
        db.Brands.Add(brand);
        db.Products.Add(product);
        db.PreOrderCapacities.Add(capacity.Capacity);
        db.PreOrderCapacityMovements.Add(capacity.Movement);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (customerId, product.Id);
    }
}
