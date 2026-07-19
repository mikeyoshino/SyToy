using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Addresses.SavedAddresses.DeleteSavedAddress;

public sealed record DeleteSavedAddressCommand(Guid AddressId) : AuthorizedSavedAddressRequest<Result>
{
    public override Result CreateFailure(Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result.Failure(requestError, validationFailures);
}
