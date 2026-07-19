using ToyStore.Application.Orders;

namespace ToyStore.Web.Components.Admin.Primitives;

public static class AdminOrderPresentation
{
    public static string SaleTypeLabel(object value) => value.ToString() switch
    {
        "InStock" => "สินค้าพร้อมส่ง",
        "PreOrder" => "พรีออเดอร์",
        _ => "ไม่ทราบประเภท",
    };

    public static string PaymentLabel(AdminOrderPaymentStatus value) => value switch
    {
        AdminOrderPaymentStatus.DepositPaid => "ชำระมัดจำแล้ว",
        AdminOrderPaymentStatus.Paid => "ชำระเงินแล้ว",
        AdminOrderPaymentStatus.PartiallyRefunded => "คืนเงินบางส่วน",
        AdminOrderPaymentStatus.Refunded => "คืนเงินแล้ว",
        _ => "ไม่ทราบสถานะ",
    };

    public static AdminStatusTone PaymentTone(AdminOrderPaymentStatus value) => value switch
    {
        AdminOrderPaymentStatus.Paid or AdminOrderPaymentStatus.DepositPaid => AdminStatusTone.Success,
        AdminOrderPaymentStatus.PartiallyRefunded => AdminStatusTone.Warning,
        AdminOrderPaymentStatus.Refunded => AdminStatusTone.Neutral,
        _ => AdminStatusTone.Neutral,
    };

    public static string FulfillmentLabel(AdminOrderFulfillmentStatus value) => value switch
    {
        AdminOrderFulfillmentStatus.AwaitingPreOrderArrival => "รอสินค้าพรีออเดอร์เข้า",
        AdminOrderFulfillmentStatus.AwaitingBalancePayment => "รอชำระยอดคงเหลือ",
        AdminOrderFulfillmentStatus.ReadyToShip => "พร้อมจัดส่ง",
        AdminOrderFulfillmentStatus.Shipped => "จัดส่งแล้ว",
        AdminOrderFulfillmentStatus.Cancelled => "ยกเลิกแล้ว",
        _ => "ไม่ทราบสถานะ",
    };

    public static AdminStatusTone FulfillmentTone(AdminOrderFulfillmentStatus value) => value switch
    {
        AdminOrderFulfillmentStatus.ReadyToShip => AdminStatusTone.Warning,
        AdminOrderFulfillmentStatus.Shipped => AdminStatusTone.Success,
        AdminOrderFulfillmentStatus.Cancelled => AdminStatusTone.Danger,
        AdminOrderFulfillmentStatus.AwaitingPreOrderArrival or AdminOrderFulfillmentStatus.AwaitingBalancePayment => AdminStatusTone.Info,
        _ => AdminStatusTone.Neutral,
    };

    public static string PaymentPurposeLabel(AdminOrderPaymentPurpose value) => value switch
    {
        AdminOrderPaymentPurpose.Deposit => "มัดจำ",
        AdminOrderPaymentPurpose.Full => "ชำระเต็มจำนวน",
        AdminOrderPaymentPurpose.Balance => "ยอดคงเหลือ",
        AdminOrderPaymentPurpose.Refund => "คืนเงิน",
        _ => "รายการชำระเงิน",
    };
}
