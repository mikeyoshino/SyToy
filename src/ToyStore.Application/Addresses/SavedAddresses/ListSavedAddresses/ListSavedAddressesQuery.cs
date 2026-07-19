using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Addresses.SavedAddresses.ListSavedAddresses;

public sealed record ListSavedAddressesQuery
    : AuthorizedSavedAddressRequest<Result<IReadOnlyList<SavedAddressView>>>
{
    public override Result<IReadOnlyList<SavedAddressView>> CreateFailure(Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<IReadOnlyList<SavedAddressView>>.Failure(requestError, validationFailures);
}
