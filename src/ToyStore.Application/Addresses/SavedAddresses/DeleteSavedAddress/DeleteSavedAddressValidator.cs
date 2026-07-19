using FluentValidation;

namespace ToyStore.Application.Addresses.SavedAddresses.DeleteSavedAddress;

public sealed class DeleteSavedAddressValidator : AbstractValidator<DeleteSavedAddressCommand>
{
    public DeleteSavedAddressValidator() => RuleFor(x => x.AddressId).NotEmpty()
        .WithMessage("ไม่พบที่อยู่ที่ต้องการลบ");
}
