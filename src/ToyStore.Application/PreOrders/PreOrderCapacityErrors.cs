using ToyStore.Application.Common.Models;

namespace ToyStore.Application.PreOrders;

public static class PreOrderCapacityErrors
{
    public static readonly Error NotAvailable = new("PreOrderCapacity.NotAvailable", "สินค้านี้ไม่พร้อมสำหรับพรีออเดอร์", ErrorType.Conflict);
    public static readonly Error NotFound = new("PreOrderCapacity.NotFound", "ไม่พบรอบพรีออเดอร์ที่ต้องการ", ErrorType.NotFound);
    public static readonly Error ReservationNotFound = new("PreOrderCapacity.ReservationNotFound", "ไม่พบการจองพรีออเดอร์ที่ต้องการ", ErrorType.NotFound);
    public static readonly Error Closed = new("PreOrderCapacity.Closed", "สินค้าพรีออเดอร์ปิดรับแล้ว", ErrorType.Conflict);
    public static readonly Error InsufficientCapacity = new("PreOrderCapacity.InsufficientCapacity", "จำนวนพรีออเดอร์คงเหลือไม่เพียงพอ", ErrorType.Conflict);
    public static readonly Error CustomerLimitExceeded = new("PreOrderCapacity.CustomerLimitExceeded", "จำนวนที่ขอเกินสิทธิ์สูงสุดต่อคน", ErrorType.Conflict);
    public static readonly Error StaleVersion = new("PreOrderCapacity.StaleVersion", "ข้อมูลพรีออเดอร์มีการเปลี่ยนแปลง กรุณาลองอีกครั้ง", ErrorType.Conflict);
    public static readonly Error OperationConflict = new("PreOrderCapacity.OperationConflict", "รหัสการทำรายการนี้ถูกใช้กับข้อมูลอื่นแล้ว", ErrorType.Conflict);
    public static readonly Error InvalidTransition = new("PreOrderCapacity.InvalidTransition", "สถานะการจองไม่รองรับการทำรายการนี้", ErrorType.Conflict);
    public static readonly Error ExpireTooEarly = new("PreOrderCapacity.ExpireTooEarly", "ยังไม่ถึงเวลาหมดอายุการจอง", ErrorType.Conflict);
    public static readonly Error InvalidExpiry = new("PreOrderCapacity.InvalidExpiry", "เวลาหมดอายุการจองไม่ถูกต้อง", ErrorType.Validation);
    public static readonly Error QuantityOverflow = new("PreOrderCapacity.QuantityOverflow", "จำนวนพรีออเดอร์สูงเกินขอบเขตที่ระบบรองรับ", ErrorType.Conflict);
    public static readonly Error VersionExhausted = new("PreOrderCapacity.VersionExhausted", "ไม่สามารถแก้ไขรอบพรีออเดอร์นี้ต่อได้ กรุณาติดต่อผู้ดูแลระบบ", ErrorType.Failure);
}
