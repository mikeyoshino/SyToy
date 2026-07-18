using FluentValidation;

namespace ToyStore.Application.Inventory.ListStockMovements;

public sealed class ListStockMovementsValidator
    : AbstractValidator<ListStockMovementsQuery>
{
    public ListStockMovementsValidator()
    {
        RuleFor(query => query.InventoryItemId)
            .NotEmpty().WithMessage("รหัสสต็อกสินค้าไม่ถูกต้อง");
        RuleFor(query => query.ProductId)
            .NotEmpty().WithMessage("รหัสสินค้าไม่ถูกต้อง");
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1).WithMessage("หน้าต้องมีค่าตั้งแต่ 1 ขึ้นไป");
        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("จำนวนรายการต่อหน้าต้องอยู่ระหว่าง 1–100");
    }
}
