using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Cart;

public static class CartErrors
{
    public static readonly Error ProductUnavailable = new(
        "Cart.ProductUnavailable",
        "สินค้านี้ไม่พร้อมเพิ่มลงตะกร้า กรุณาเลือกสินค้าอื่น",
        ErrorType.Conflict);

    public static readonly Error CartNotFound = new(
        "Cart.NotFound",
        "ไม่พบตะกร้าสินค้า กรุณารีเฟรชแล้วลองอีกครั้ง",
        ErrorType.NotFound);

    public static readonly Error ItemNotFound = new(
        "Cart.ItemNotFound",
        "ไม่พบสินค้านี้ในตะกร้า กรุณารีเฟรชแล้วลองอีกครั้ง",
        ErrorType.NotFound);

    public static readonly Error QuantityExceedsLimit = new(
        "Cart.QuantityExceedsLimit",
        "สินค้าแต่ละรายการในตะกร้ามีได้ไม่เกิน 99 ชิ้น",
        ErrorType.Validation);

    public static readonly Error StaleVersion = new(
        "Cart.StaleVersion",
        "ตะกร้ามีการเปลี่ยนแปลง กรุณารีเฟรชแล้วลองอีกครั้ง",
        ErrorType.Conflict);

    public static readonly Error OwnershipMismatch = new(
        "Cart.OwnershipMismatch",
        "คุณไม่มีสิทธิ์เข้าถึงตะกร้านี้",
        ErrorType.Forbidden);

    public static readonly Error OperationConflict = new(
        "Cart.OperationConflict",
        "คำขอนี้เคยถูกใช้กับข้อมูลอื่น กรุณารีเฟรชแล้วลองอีกครั้ง",
        ErrorType.Conflict);

    public static readonly Error CustomerUnavailable = new(
        "Cart.CustomerUnavailable",
        "ไม่พบบัญชีลูกค้าที่พร้อมใช้งาน กรุณาเข้าสู่ระบบใหม่",
        ErrorType.Unauthorized);
}
