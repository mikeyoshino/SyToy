using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Addresses.SavedAddresses.SetDefaultSavedAddress;

public sealed record SetDefaultSavedAddressCommand(Guid AddressId) : AuthorizedSavedAddressRequest<Result>
{
    public override Result CreateFailure(Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result.Failure(requestError, validationFailures);
}
