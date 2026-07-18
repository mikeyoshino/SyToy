using MediatR;

namespace ToyStore.Application.Addresses.GetThaiAddressOptions;

public sealed class GetThaiAddressOptionsHandler(IThaiAddressCatalog catalog)
    : IRequestHandler<GetThaiAddressOptionsQuery, ThaiAddressOptionsResult>
{
    public Task<ThaiAddressOptionsResult> Handle(
        GetThaiAddressOptionsQuery request,
        CancellationToken cancellationToken)
    {
        var districts = request.ProvinceId is { } provinceId
            ? catalog.GetDistricts(provinceId)
            : [];
        var subDistricts = request.DistrictId is { } districtId
            ? catalog.GetSubDistricts(districtId)
            : [];

        return Task.FromResult(new ThaiAddressOptionsResult(
            catalog.Provinces,
            districts,
            subDistricts));
    }
}
