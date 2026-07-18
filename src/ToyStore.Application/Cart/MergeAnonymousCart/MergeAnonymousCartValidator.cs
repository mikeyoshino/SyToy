using FluentValidation;
using ToyStore.Domain.Carts;

namespace ToyStore.Application.Cart.MergeAnonymousCart;

public sealed class MergeAnonymousCartValidator : AbstractValidator<MergeAnonymousCartCommand>
{
    public MergeAnonymousCartValidator()
    {
        RuleFor(command => command.OperationId).NotEmpty().WithMessage("รหัสคำขอไม่ถูกต้อง");
        RuleFor(command => command.Items).NotNull().WithMessage("ไม่พบรายการตะกร้าที่ต้องรวม")
            .NotEmpty().WithMessage("กรุณาเลือกรายการสินค้าอย่างน้อย 1 รายการ");
        RuleForEach(command => command.Items).ChildRules(item =>
        {
            item.RuleFor(value => value.ProductId).NotEmpty().WithMessage("สินค้าที่เลือกไม่ถูกต้อง");
            item.RuleFor(value => value.Quantity)
                .InclusiveBetween(1, CartLimits.MaximumQuantityPerItem)
                .WithMessage("จำนวนสินค้าต้องอยู่ระหว่าง 1–99 ชิ้น");
        });
    }
}
