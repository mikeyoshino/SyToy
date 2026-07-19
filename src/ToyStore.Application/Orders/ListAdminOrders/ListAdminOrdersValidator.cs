using FluentValidation;

namespace ToyStore.Application.Orders.ListAdminOrders;

public sealed class ListAdminOrdersValidator : AbstractValidator<ListAdminOrdersQuery>
{
    public ListAdminOrdersValidator()
    {
        RuleFor(query => query.Search)
            .MaximumLength(200)
            .WithMessage("คำค้นหาต้องไม่เกิน 200 ตัวอักษร");
        RuleFor(query => query.SaleType)
            .Must(value => value is null || Enum.IsDefined(value.Value))
            .WithMessage("ประเภทการขายไม่ถูกต้อง");
        RuleFor(query => query.PaymentStatus)
            .Must(value => value is null || Enum.IsDefined(value.Value))
            .WithMessage("สถานะการชำระเงินไม่ถูกต้อง");
        RuleFor(query => query.FulfillmentStatus)
            .Must(value => value is null || Enum.IsDefined(value.Value))
            .WithMessage("สถานะการจัดส่งไม่ถูกต้อง");
        RuleFor(query => query)
            .Must(query => query.CreatedFrom is null || query.CreatedTo is null
                || query.CreatedFrom <= query.CreatedTo)
            .WithName(nameof(ListAdminOrdersQuery.CreatedTo))
            .WithMessage("วันที่สิ้นสุดต้องไม่ก่อนวันที่เริ่มต้น");
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("หน้าต้องมีค่าตั้งแต่ 1 ขึ้นไป");
        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("จำนวนรายการต่อหน้าต้องอยู่ระหว่าง 1–100");
    }
}
