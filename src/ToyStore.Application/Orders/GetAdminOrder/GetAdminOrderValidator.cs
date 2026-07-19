using FluentValidation;

namespace ToyStore.Application.Orders.GetAdminOrder;

public sealed class GetAdminOrderValidator : AbstractValidator<GetAdminOrderQuery>
{
    public GetAdminOrderValidator()
    {
        RuleFor(query => query.OrderNumber)
            .NotEmpty().WithMessage("กรุณาระบุเลขคำสั่งซื้อ")
            .MaximumLength(40).WithMessage("เลขคำสั่งซื้อต้องไม่เกิน 40 ตัวอักษร");
    }
}
