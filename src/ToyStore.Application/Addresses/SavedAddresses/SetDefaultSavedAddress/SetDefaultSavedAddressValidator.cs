using FluentValidation;

namespace ToyStore.Application.Addresses.SavedAddresses.SetDefaultSavedAddress;

public sealed class SetDefaultSavedAddressValidator : AbstractValidator<SetDefaultSavedAddressCommand>
{
    public SetDefaultSavedAddressValidator() => RuleFor(x => x.AddressId).NotEmpty()
        .WithMessage("ไม่พบที่อยู่ที่ต้องการตั้งเป็นค่าเริ่มต้น");
}
