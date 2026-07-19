using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Orders;
using ToyStore.Application.Orders.GetAdminOrder;
using ToyStore.Application.Orders.ListAdminOrders;

namespace ToyStore.UnitTests.Application.Orders;

public sealed class AdminOrderQueryContractTests
{
    [Fact]
    public void QueriesRequireOrderPolicyAndValidatorsReturnThaiFailures()
    {
        var list = new ListAdminOrdersQuery(
            new string('ก', 201),
            (AdminOrderSaleType)99,
            (AdminOrderPaymentStatus)99,
            (AdminOrderFulfillmentStatus)99,
            new DateOnly(2026, 7, 20),
            new DateOnly(2026, 7, 19),
            0,
            101);
        var detail = new GetAdminOrderQuery(string.Empty);

        var failures = new ListAdminOrdersValidator().Validate(list).Errors;
        var detailFailures = new GetAdminOrderValidator().Validate(detail).Errors;

        Assert.Equal(PolicyNames.CanManageOrders, list.RequiredPolicy);
        Assert.Equal(PolicyNames.CanManageOrders, detail.RequiredPolicy);
        Assert.Contains(failures, failure => failure.ErrorMessage.Contains("คำค้นหา", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.ErrorMessage.Contains("ประเภทการขาย", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.ErrorMessage.Contains("สถานะการชำระเงิน", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.ErrorMessage.Contains("สถานะการจัดส่ง", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.ErrorMessage.Contains("วันที่สิ้นสุด", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.ErrorMessage.Contains("หน้า", StringComparison.Ordinal));
        Assert.Contains(detailFailures, failure => failure.ErrorMessage.Contains("เลขคำสั่งซื้อ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListHandlerNormalizesSearchAndConvertsBangkokDateRangeToUtc()
    {
        var reader = new CapturingReader();
        var handler = new ListAdminOrdersHandler(reader);

        var result = await handler.Handle(new ListAdminOrdersQuery(
            "  SY   ลูกค้า  ",
            AdminOrderSaleType.InStock,
            AdminOrderPaymentStatus.Paid,
            AdminOrderFulfillmentStatus.ReadyToShip,
            new DateOnly(2026, 7, 19),
            new DateOnly(2026, 7, 20),
            2,
            20), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(reader.Request);
        Assert.Equal("SY ลูกค้า", reader.Request.Search);
        Assert.Equal(new DateTimeOffset(2026, 7, 18, 17, 0, 0, TimeSpan.Zero), reader.Request.CreatedFromUtc);
        Assert.Equal(new DateTimeOffset(2026, 7, 20, 17, 0, 0, TimeSpan.Zero), reader.Request.CreatedBeforeUtc);
        Assert.Equal(2, reader.Request.Page);
    }

    private sealed class CapturingReader : IAdminOrderReader
    {
        public AdminOrderReadRequest? Request { get; private set; }

        public Task<AdminOrderPage> ListAsync(AdminOrderReadRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new AdminOrderPage([], request.Page, request.PageSize, 0));
        }

        public Task<AdminOrderDetailView?> GetAsync(string orderNumber, CancellationToken cancellationToken) =>
            Task.FromResult<AdminOrderDetailView?>(null);
    }
}
