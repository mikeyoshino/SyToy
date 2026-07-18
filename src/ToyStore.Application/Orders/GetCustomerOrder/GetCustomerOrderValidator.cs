using FluentValidation;

namespace ToyStore.Application.Orders.GetCustomerOrder;

public sealed class GetCustomerOrderValidator : AbstractValidator<GetCustomerOrderQuery>
{
    public GetCustomerOrderValidator()
    {
        RuleFor(query => query.OrderNumber)
            .NotEmpty().WithMessage("กรุณาระบุเลขคำสั่งซื้อ")
            .MaximumLength(40).WithMessage("เลขคำสั่งซื้อต้องไม่เกิน 40 ตัวอักษร");
    }
}
