using FluentValidation;

namespace ToyStore.Application.Checkout.BeginPreOrderCheckout;

public sealed class BeginPreOrderCheckoutValidator : AbstractValidator<BeginPreOrderCheckoutCommand>
{
    public BeginPreOrderCheckoutValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("รหัสสินค้าไม่ถูกต้อง");
        RuleFor(x => x.ClientRequestId).NotEmpty().WithMessage("รหัสการทำรายการไม่ถูกต้อง");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("จำนวนต้องมากกว่า 0");
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(200).WithMessage("กรุณาระบุชื่อผู้รับไม่เกิน 200 ตัวอักษร");
        RuleFor(x => x.PhoneNumber).Matches("^[0-9+ -]{8,20}$").WithMessage("กรุณาระบุหมายเลขโทรศัพท์ให้ถูกต้อง");
        RuleFor(x => x.AddressLine).NotEmpty().MaximumLength(500).WithMessage("กรุณาระบุบ้านเลขที่และรายละเอียดที่อยู่");
        RuleFor(x => x.SubDistrict).NotEmpty().MaximumLength(200).WithMessage("กรุณาระบุตำบลหรือแขวง");
        RuleFor(x => x.District).NotEmpty().MaximumLength(200).WithMessage("กรุณาระบุอำเภอหรือเขต");
        RuleFor(x => x.Province).NotEmpty().MaximumLength(200).WithMessage("กรุณาระบุจังหวัด");
        RuleFor(x => x.PostalCode).Matches("^[0-9]{5}$").WithMessage("รหัสไปรษณีย์ต้องเป็นตัวเลข 5 หลัก");
    }
}
