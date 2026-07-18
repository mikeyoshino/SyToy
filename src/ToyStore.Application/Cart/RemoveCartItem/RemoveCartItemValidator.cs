using FluentValidation;

namespace ToyStore.Application.Cart.RemoveCartItem;

public sealed class RemoveCartItemValidator : AbstractValidator<RemoveCartItemCommand>
{
    public RemoveCartItemValidator()
    {
        RuleFor(command => command.OperationId).NotEmpty().WithMessage("รหัสคำขอไม่ถูกต้อง");
        RuleFor(command => command.ProductId).NotEmpty().WithMessage("สินค้าที่เลือกไม่ถูกต้อง");
        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(1)
            .WithMessage("รุ่นข้อมูลตะกร้าไม่ถูกต้อง");
    }
}
