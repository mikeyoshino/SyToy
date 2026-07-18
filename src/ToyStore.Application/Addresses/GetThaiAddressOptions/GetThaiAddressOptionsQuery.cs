using MediatR;

namespace ToyStore.Application.Addresses.GetThaiAddressOptions;

public sealed record GetThaiAddressOptionsQuery(int? ProvinceId = null, int? DistrictId = null)
    : IRequest<ThaiAddressOptionsResult>;

public sealed record ThaiAddressOptionsResult(
    IReadOnlyList<ThaiProvince> Provinces,
    IReadOnlyList<ThaiDistrict> Districts,
    IReadOnlyList<ThaiSubDistrict> SubDistricts);
