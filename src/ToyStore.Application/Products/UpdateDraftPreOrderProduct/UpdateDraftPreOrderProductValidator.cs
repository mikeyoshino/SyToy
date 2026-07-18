using FluentValidation;

namespace ToyStore.Application.Products.UpdateDraftPreOrderProduct;

public sealed class UpdateDraftPreOrderProductValidator
    : AbstractValidator<UpdateDraftPreOrderProductCommand>
{
    public UpdateDraftPreOrderProductValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("รหัสสินค้าไม่ถูกต้อง");
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("เวอร์ชันข้อมูลสินค้าไม่ถูกต้อง");
        RuleFor(x => x.DisplayName).Custom(InStockProductValidationRules.ValidateDisplayName);
        RuleFor(x => x.EnglishName).Custom(InStockProductValidationRules.ValidateEnglishName);
        RuleFor(x => x.Description).NotEmpty().WithMessage("กรุณากรอกคำอธิบายสินค้า");
        RuleFor(x => x.ModelScale).Custom(InStockProductValidationRules.ValidateModelScale);
        RuleFor(x => x.ProductCategoryId).Must(InStockProductValidationRules.IsAllowedCategory)
            .WithMessage("กรุณาเลือกหมวดหมู่ Art Toy หรือ Gundam");
        RuleFor(x => x.BrandId).NotEmpty().WithMessage("กรุณาเลือกแบรนด์");
        RuleFor(x => x.UniverseId).NotEmpty().WithMessage("กรุณาเลือกจักรวาล");
        RuleFor(x => x.CharacterIds).Custom(InStockProductValidationRules.ValidateCharacters);
        RuleFor(x => x.FullPrice).GreaterThan(0).WithMessage("ราคาเต็มต้องมากกว่า 0 บาท");
        RuleFor(x => x.DepositAmount).GreaterThan(0).WithMessage("เงินมัดจำต้องมากกว่า 0 บาท")
            .LessThan(x => x.FullPrice).WithMessage("เงินมัดจำต้องน้อยกว่าราคาเต็ม");
        RuleFor(x => x.EstimatedArrivalMonth).InclusiveBetween(1, 12)
            .WithMessage("เดือนที่สินค้าคาดว่าจะมาถึงไม่ถูกต้อง");
        RuleFor(x => x.EstimatedArrivalYear).InclusiveBetween(1, 9999)
            .WithMessage("ปีที่สินค้าคาดว่าจะมาถึงไม่ถูกต้อง");
        RuleFor(x => x.TotalCapacity).GreaterThan(0).WithMessage("จำนวนรับพรีออเดอร์ต้องมากกว่า 0");
        RuleFor(x => x.MaxPerCustomer).GreaterThan(0).WithMessage("จำนวนสูงสุดต่อคนต้องมากกว่า 0")
            .LessThanOrEqualTo(x => x.TotalCapacity)
            .WithMessage("จำนวนสูงสุดต่อคนต้องไม่เกินจำนวนรับพรีออเดอร์");
        RuleFor(x => x.BalancePaymentDays).GreaterThan(0)
            .WithMessage("ระยะเวลาชำระยอดคงเหลือต้องมากกว่า 0 วัน");
        RuleFor(x => x.Images).Custom((slots, context) =>
            InStockProductValidationRules.ValidateMedia(slots, context, true));
        PreOrderProductValidationRules.AddTemporalRules(
            this, timeProvider, x => x.CloseDate, x => x.CloseDate,
            x => x.EstimatedArrivalMonth, x => x.EstimatedArrivalYear);
    }
}
