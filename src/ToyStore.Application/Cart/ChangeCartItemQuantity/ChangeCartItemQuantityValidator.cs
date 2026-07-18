using FluentValidation;
using ToyStore.Domain.Carts;

namespace ToyStore.Application.Cart.ChangeCartItemQuantity;

public sealed class ChangeCartItemQuantityValidator
    : AbstractValidator<ChangeCartItemQuantityCommand>
{
    public ChangeCartItemQuantityValidator()
    {
        RuleFor(command => command.OperationId).NotEmpty().WithMessage("รหัสคำขอไม่ถูกต้อง");
        RuleFor(command => command.ProductId).NotEmpty().WithMessage("สินค้าที่เลือกไม่ถูกต้อง");
        RuleFor(command => command.Quantity)
            .InclusiveBetween(1, CartLimits.MaximumQuantityPerItem)
            .WithMessage("จำนวนสินค้าต้องอยู่ระหว่าง 1–99 ชิ้น");
        RuleFor(command => command.ExpectedVersion)
            .GreaterThanOrEqualTo(1).WithMessage("รุ่นข้อมูลตะกร้าไม่ถูกต้อง");
    }
}
