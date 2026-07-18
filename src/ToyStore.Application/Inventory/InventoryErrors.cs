using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Inventory;

public static class InventoryErrors
{
    public static readonly Error NotFound = new(
        "Inventory.NotFound",
        "ไม่พบข้อมูลสต็อกสินค้าที่ต้องการ",
        ErrorType.NotFound);

    public static readonly Error StaleVersion = new(
        "Inventory.StaleVersion",
        "ข้อมูลสต็อกมีการเปลี่ยนแปลง กรุณารีเฟรชแล้วลองอีกครั้ง",
        ErrorType.Conflict);

    public static readonly Error InsufficientOnHand = new(
        "Inventory.InsufficientOnHand",
        "ไม่สามารถปรับลดต่ำกว่าจำนวนสินค้าที่ถูกจองไว้ได้",
        ErrorType.Conflict);

    public static readonly Error OperationConflict = new(
        "Inventory.OperationConflict",
        "รหัสการทำรายการนี้ถูกใช้กับข้อมูลอื่นแล้ว",
        ErrorType.Conflict);

    public static readonly Error QuantityOverflow = new(
        "Inventory.QuantityOverflow",
        "จำนวนสินค้าสูงเกินขอบเขตที่ระบบรองรับ",
        ErrorType.Conflict);

    public static readonly Error VersionExhausted = new(
        "Inventory.VersionExhausted",
        "ไม่สามารถแก้ไขสต็อกนี้ต่อได้ กรุณาติดต่อผู้ดูแลระบบ",
        ErrorType.Failure);
}
