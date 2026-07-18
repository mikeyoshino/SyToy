using FluentValidation;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Storefront.Catalog;

public sealed class ListStorefrontProductsValidator : AbstractValidator<ListStorefrontProductsQuery>
{
    public ListStorefrontProductsValidator()
    {
        RuleFor(query => query.Search).MaximumLength(CatalogReferenceLimits.NameLength)
            .WithMessage("คำค้นหาต้องไม่เกิน 200 ตัวอักษร");
        RuleFor(query => query.SaleType).IsInEnum().WithMessage("ประเภทการขายไม่ถูกต้อง");
        RuleFor(query => query.BrandSlug)
            .Must(slug => string.IsNullOrWhiteSpace(slug)
                || CatalogSlug.IsValid(slug.Trim().ToLowerInvariant()))
            .WithMessage("ส่วน URL ของแบรนด์ไม่ถูกต้อง");
        RuleFor(query => query.ProductCategoryId).Must(ValidOptionalId).WithMessage("หมวดหมู่ไม่ถูกต้อง");
        RuleFor(query => query.BrandId).Must(ValidOptionalId).WithMessage("แบรนด์ไม่ถูกต้อง");
        RuleFor(query => query.CharacterId).Must(ValidOptionalId).WithMessage("ตัวละครไม่ถูกต้อง");
        RuleFor(query => query.UniverseId).Must(ValidOptionalId).WithMessage("จักรวาลไม่ถูกต้อง");
        RuleFor(query => query.MinimumPrice).GreaterThanOrEqualTo(0).When(query => query.MinimumPrice.HasValue)
            .WithMessage("ราคาต่ำสุดต้องไม่ติดลบ");
        RuleFor(query => query.MaximumPrice).GreaterThanOrEqualTo(0).When(query => query.MaximumPrice.HasValue)
            .WithMessage("ราคาสูงสุดต้องไม่ติดลบ");
        RuleFor(query => query).Must(query => !query.MinimumPrice.HasValue || !query.MaximumPrice.HasValue
            || query.MinimumPrice <= query.MaximumPrice).WithMessage("ช่วงราคาไม่ถูกต้อง");
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1).WithMessage("หน้าต้องมีค่าตั้งแต่ 1 ขึ้นไป");
        RuleFor(query => query.PageSize).InclusiveBetween(1, 48).WithMessage("จำนวนสินค้าต่อหน้าต้องอยู่ระหว่าง 1–48");
    }

    private static bool ValidOptionalId(Guid? id) => id is null || id != Guid.Empty;
}
