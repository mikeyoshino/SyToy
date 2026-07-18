using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Orders;

public static class CustomerOrderErrors
{
    public static readonly Error NotFound = new(
        "CustomerOrder.NotFound",
        "ไม่พบคำสั่งซื้อนี้ในบัญชีของคุณ",
        ErrorType.NotFound);
}
