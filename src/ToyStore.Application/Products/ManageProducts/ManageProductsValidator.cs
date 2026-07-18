using FluentValidation;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Products.ManageProducts;

public sealed class ManageProductsValidator : AbstractValidator<ManageProductsQuery>
{
    public ManageProductsValidator()
    {
        RuleFor(query => query.Search)
            .MaximumLength(CatalogReferenceLimits.NameLength)
            .WithMessage("คำค้นหาต้องไม่เกิน 200 ตัวอักษร");
        RuleFor(query => query.Status)
            .Must(status => status is null || Enum.IsDefined(status.Value))
            .WithMessage("สถานะสินค้าไม่ถูกต้อง");
        RuleFor(query => query.ProductCategoryId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("หมวดหมู่สินค้าไม่ถูกต้อง");
        RuleFor(query => query.BrandId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("แบรนด์ไม่ถูกต้อง");
        RuleFor(query => query.UniverseId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("จักรวาลไม่ถูกต้อง");
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("หน้าต้องมีค่าตั้งแต่ 1 ขึ้นไป");
        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("จำนวนรายการต่อหน้าต้องอยู่ระหว่าง 1–100");
    }
}
