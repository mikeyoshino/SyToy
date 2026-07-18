using FluentValidation;

namespace ToyStore.Application.Products.UpdateDraftInStockProduct;

public sealed class UpdateDraftInStockProductValidator
    : AbstractValidator<UpdateDraftInStockProductCommand>
{
    public UpdateDraftInStockProductValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty()
            .WithMessage("รหัสสินค้าไม่ถูกต้อง");
        RuleFor(command => command.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("เวอร์ชันข้อมูลสินค้าไม่ถูกต้อง");
        RuleFor(command => command.DisplayName)
            .Custom(InStockProductValidationRules.ValidateDisplayName);
        RuleFor(command => command.EnglishName)
            .Custom(InStockProductValidationRules.ValidateEnglishName);
        RuleFor(command => command.Description)
            .NotEmpty()
            .WithMessage("กรุณากรอกคำอธิบายสินค้า");
        RuleFor(command => command.ModelScale)
            .Custom(InStockProductValidationRules.ValidateModelScale);
        RuleFor(command => command.ProductCategoryId)
            .Must(InStockProductValidationRules.IsAllowedCategory)
            .WithMessage("กรุณาเลือกหมวดหมู่ Art Toy หรือ Gundam");
        RuleFor(command => command.BrandId)
            .NotEmpty()
            .WithMessage("กรุณาเลือกแบรนด์");
        RuleFor(command => command.UniverseId)
            .NotEmpty()
            .WithMessage("กรุณาเลือกจักรวาล");
        RuleFor(command => command.CharacterIds)
            .Custom(InStockProductValidationRules.ValidateCharacters);
        RuleFor(command => command.Price)
            .GreaterThan(0)
            .WithMessage("ราคาสินค้าต้องมากกว่า 0 บาท");
        RuleFor(command => command.Images)
            .Custom((slots, context) => InStockProductValidationRules.ValidateMedia(
                slots,
                context,
                allowRetained: true));
    }
}
