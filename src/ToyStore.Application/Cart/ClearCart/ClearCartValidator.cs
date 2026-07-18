using FluentValidation;

namespace ToyStore.Application.Cart.ClearCart;

public sealed class ClearCartValidator : AbstractValidator<ClearCartCommand>
{
    public ClearCartValidator()
    {
        RuleFor(command => command.OperationId).NotEmpty().WithMessage("รหัสคำขอไม่ถูกต้อง");
        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(1)
            .WithMessage("รุ่นข้อมูลตะกร้าไม่ถูกต้อง");
    }
}
