using FluentValidation;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Universes.ListUniverses;

public sealed class ListUniversesValidator : AbstractValidator<ListUniversesQuery>
{
    public ListUniversesValidator()
    {
        RuleFor(query => query.Search)
            .MaximumLength(CatalogReferenceLimits.NameLength)
            .WithMessage("คำค้นหาต้องไม่เกิน 200 ตัวอักษร");

        RuleFor(query => query.Status)
            .IsInEnum()
            .WithMessage("สถานะจักรวาลไม่ถูกต้อง");

        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("หน้าต้องมีค่าตั้งแต่ 1 ขึ้นไป");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("จำนวนรายการต่อหน้าต้องอยู่ระหว่าง 1–100");
    }
}
