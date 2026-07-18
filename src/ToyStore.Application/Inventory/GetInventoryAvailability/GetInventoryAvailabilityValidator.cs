using FluentValidation;

namespace ToyStore.Application.Inventory.GetInventoryAvailability;

public sealed class GetInventoryAvailabilityValidator
    : AbstractValidator<GetInventoryAvailabilityQuery>
{
    public GetInventoryAvailabilityValidator()
    {
        RuleFor(query => query.InventoryItemId)
            .NotEmpty().WithMessage("รหัสสต็อกสินค้าไม่ถูกต้อง");
        RuleFor(query => query.ProductId)
            .NotEmpty().WithMessage("รหัสสินค้าไม่ถูกต้อง");
    }
}
