using FluentValidation;

namespace ToyStore.Application.Products.ArchiveProduct;

public sealed class ArchiveProductValidator : AbstractValidator<ArchiveProductCommand>
{
    public ArchiveProductValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty()
            .WithMessage("รหัสสินค้าไม่ถูกต้อง");
        RuleFor(command => command.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("เวอร์ชันข้อมูลสินค้าไม่ถูกต้อง");
    }
}
