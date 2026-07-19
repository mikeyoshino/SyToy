using FluentValidation;

namespace ToyStore.Application.Addresses.SavedAddresses.CreateSavedAddress;

public sealed class CreateSavedAddressValidator : AbstractValidator<CreateSavedAddressCommand>
{
    public CreateSavedAddressValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(80)
            .WithMessage("กรุณาตั้งชื่อที่อยู่ไม่เกิน 80 ตัวอักษร");
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(200)
            .WithMessage("กรุณาระบุชื่อผู้รับไม่เกิน 200 ตัวอักษร");
        RuleFor(x => x.PhoneNumber).Matches("^[0-9+ -]{8,20}$")
            .WithMessage("กรุณาระบุหมายเลขโทรศัพท์ให้ถูกต้อง");
        RuleFor(x => x.AddressLine).NotEmpty().MaximumLength(500)
            .WithMessage("กรุณาระบุบ้านเลขที่และรายละเอียดที่อยู่");
        RuleFor(x => x.ProvinceId).GreaterThan(0).WithMessage("กรุณาเลือกจังหวัด");
        RuleFor(x => x.DistrictId).GreaterThan(0).WithMessage("กรุณาเลือกอำเภอหรือเขต");
        RuleFor(x => x.SubDistrictId).GreaterThan(0).WithMessage("กรุณาเลือกตำบลหรือแขวง");
        RuleFor(x => x.Province).NotEmpty().MaximumLength(200).WithMessage("กรุณาระบุจังหวัด");
        RuleFor(x => x.District).NotEmpty().MaximumLength(200).WithMessage("กรุณาระบุอำเภอหรือเขต");
        RuleFor(x => x.SubDistrict).NotEmpty().MaximumLength(200).WithMessage("กรุณาระบุตำบลหรือแขวง");
        RuleFor(x => x.PostalCode).Matches("^[0-9]{5}$")
            .WithMessage("รหัสไปรษณีย์ต้องเป็นตัวเลข 5 หลัก");
    }
}
