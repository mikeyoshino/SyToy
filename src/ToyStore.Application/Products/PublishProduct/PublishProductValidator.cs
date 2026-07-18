using FluentValidation;

namespace ToyStore.Application.Products.PublishProduct;

public sealed class PublishProductValidator : AbstractValidator<PublishProductCommand>
{
    public PublishProductValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty()
            .WithMessage("รหัสสินค้าไม่ถูกต้อง");
        RuleFor(command => command.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("เวอร์ชันข้อมูลสินค้าไม่ถูกต้อง");
    }
}
