using FluentValidation;

namespace ToyStore.Application.Orders.ListCustomerOrders;

public sealed class ListCustomerOrdersValidator : AbstractValidator<ListCustomerOrdersQuery>
{
    public ListCustomerOrdersValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("หน้ารายการต้องเริ่มจากหน้า 1");
        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("จำนวนรายการต่อหน้าต้องอยู่ระหว่าง 1–50 รายการ");
        RuleFor(query => query.SearchTerm)
            .MaximumLength(100)
            .WithMessage("คำค้นหาต้องไม่เกิน 100 ตัวอักษร");
    }
}
