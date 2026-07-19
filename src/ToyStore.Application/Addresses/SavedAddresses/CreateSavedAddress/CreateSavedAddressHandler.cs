using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Addresses;
using ToyStore.Domain.Checkouts;

namespace ToyStore.Application.Addresses.SavedAddresses.CreateSavedAddress;

public sealed class CreateSavedAddressHandler(
    ISavedAddressStore store,
    IThaiAddressCatalog addressCatalog,
    TimeProvider timeProvider)
    : IRequestHandler<CreateSavedAddressCommand, Result<SavedAddressView>>
{
    public Task<Result<SavedAddressView>> Handle(CreateSavedAddressCommand request,
        CancellationToken cancellationToken)
    {
        var customerId = request.AuthorizedActorId
            ?? throw new InvalidOperationException("Saved address command requires an authorized customer.");
        var provinceMatches = addressCatalog.Provinces.Any(province =>
            province.Id == request.ProvinceId && province.Name == request.Province.Trim());
        var districtMatches = addressCatalog.GetDistricts(request.ProvinceId).Any(district =>
            district.Id == request.DistrictId && district.Name == request.District.Trim());
        var subDistrictMatches = addressCatalog.GetSubDistricts(request.DistrictId).Any(subDistrict =>
            subDistrict.Id == request.SubDistrictId
            && subDistrict.Name == request.SubDistrict.Trim()
            && subDistrict.PostalCode == request.PostalCode.Trim());
        if (!provinceMatches || !districtMatches || !subDistrictMatches
            || !addressCatalog.IsValid(request.Province, request.District,
                request.SubDistrict, request.PostalCode))
            return Task.FromResult(Result<SavedAddressView>.Failure(SavedAddressErrors.AddressInvalid));

        var address = ShippingAddressSnapshot.Create(request.RecipientName, request.PhoneNumber,
            request.AddressLine, request.SubDistrict, request.District, request.Province,
            request.PostalCode);
        var saved = SavedAddress.Create(Guid.NewGuid(), customerId, request.Label, address,
            request.ProvinceId, request.DistrictId, request.SubDistrictId,
            request.MakeDefault, timeProvider.GetUtcNow());
        return store.CreateAsync(saved, cancellationToken);
    }
}
