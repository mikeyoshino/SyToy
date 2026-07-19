using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Orders;

public static class AdminOrderErrors
{
    public static readonly Error NotFound = new(
        "AdminOrder.NotFound",
        "ไม่พบคำสั่งซื้อนี้",
        ErrorType.NotFound);
    public static readonly Error InvalidShipmentState = new("AdminOrder.InvalidShipmentState", "คำสั่งซื้อนี้ไม่อยู่ในสถานะพร้อมจัดส่ง", ErrorType.Conflict);
    public static readonly Error Stale = new("AdminOrder.Stale", "ข้อมูลคำสั่งซื้อเปลี่ยนแล้ว กรุณาโหลดใหม่", ErrorType.Conflict);
    public static readonly Error ShipmentConflict = new("AdminOrder.ShipmentConflict", "คำสั่งซื้อนี้มีข้อมูลจัดส่งแล้ว", ErrorType.Conflict);
}
