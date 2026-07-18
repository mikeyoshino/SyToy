using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Checkout;

public static class CheckoutErrors
{
    public static readonly Error NotAvailable = new("Checkout.NotAvailable", "สินค้านี้ไม่พร้อมสำหรับการสั่งซื้อ", ErrorType.Conflict);
    public static readonly Error AddressInvalid = new("Checkout.AddressInvalid", "ที่อยู่จัดส่งไม่ถูกต้อง กรุณาตรวจสอบอีกครั้ง", ErrorType.Validation);
    public static readonly Error CustomerEmailMissing = new("Checkout.CustomerEmailMissing", "ไม่พบอีเมลของบัญชี กรุณาตรวจสอบข้อมูลบัญชีอีกครั้ง", ErrorType.Validation);
    public static readonly Error PaymentUnavailable = new("Checkout.PaymentUnavailable", "ระบบชำระเงินยังไม่พร้อม กรุณาลองใหม่อีกครั้ง", ErrorType.Failure);
    public static readonly Error PaymentMismatch = new("Checkout.PaymentMismatch", "ข้อมูลการชำระเงินไม่ตรงกับรายการสั่งซื้อ", ErrorType.Conflict);
    public static readonly Error NotFound = new("Checkout.NotFound", "ไม่พบรายการชำระเงินนี้", ErrorType.NotFound);
    public static readonly Error CartEmpty = new("Checkout.CartEmpty", "ไม่มีสินค้าในตะกร้าสำหรับชำระเงิน", ErrorType.Validation);
    public static readonly Error StockInsufficient = new("Checkout.StockInsufficient", "สินค้าบางรายการมีจำนวนไม่เพียงพอ กรุณาตรวจสอบตะกร้าอีกครั้ง", ErrorType.Conflict);
}
