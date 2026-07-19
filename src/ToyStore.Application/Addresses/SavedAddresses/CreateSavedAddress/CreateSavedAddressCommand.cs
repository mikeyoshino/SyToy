using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Addresses.SavedAddresses.CreateSavedAddress;

public sealed record CreateSavedAddressCommand(
    string Label,
    string RecipientName,
    string PhoneNumber,
    string AddressLine,
    int ProvinceId,
    int DistrictId,
    int SubDistrictId,
    string Province,
    string District,
    string SubDistrict,
    string PostalCode,
    bool MakeDefault)
    : AuthorizedSavedAddressRequest<Result<SavedAddressView>>
{
    public override Result<SavedAddressView> CreateFailure(Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<SavedAddressView>.Failure(requestError, validationFailures);
}
