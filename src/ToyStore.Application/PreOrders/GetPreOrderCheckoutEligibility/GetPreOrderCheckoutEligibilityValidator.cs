using FluentValidation;

namespace ToyStore.Application.PreOrders.GetPreOrderCheckoutEligibility;

public sealed class GetPreOrderCheckoutEligibilityValidator
    : AbstractValidator<GetPreOrderCheckoutEligibilityQuery>
{
    public GetPreOrderCheckoutEligibilityValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("รหัสสินค้าไม่ถูกต้อง");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("จำนวนต้องมากกว่า 0");
    }
}
