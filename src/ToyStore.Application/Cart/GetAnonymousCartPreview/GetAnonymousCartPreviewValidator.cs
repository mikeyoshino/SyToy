using FluentValidation;
using ToyStore.Domain.Carts;

namespace ToyStore.Application.Cart.GetAnonymousCartPreview;

public sealed class GetAnonymousCartPreviewValidator : AbstractValidator<GetAnonymousCartPreviewQuery>
{
    public GetAnonymousCartPreviewValidator()
    {
        RuleFor(query => query.Items).NotNull().WithMessage("ไม่พบข้อมูลตะกร้าสินค้า");
        RuleForEach(query => query.Items).ChildRules(item =>
        {
            item.RuleFor(value => value.ProductId).NotEmpty().WithMessage("สินค้าที่เลือกไม่ถูกต้อง");
            item.RuleFor(value => value.Quantity)
                .InclusiveBetween(1, CartLimits.MaximumQuantityPerItem)
                .WithMessage("จำนวนสินค้าต้องอยู่ระหว่าง 1–99 ชิ้น");
        });
    }
}
