namespace ToyStore.Application.Addresses;

public interface IThaiAddressCatalog
{
    IReadOnlyList<ThaiProvince> Provinces { get; }

    IReadOnlyList<ThaiDistrict> GetDistricts(int provinceId);

    IReadOnlyList<ThaiSubDistrict> GetSubDistricts(int districtId);

    bool IsValid(string province, string district, string subDistrict, string postalCode);
}

public sealed record ThaiProvince(int Id, string Name);

public sealed record ThaiDistrict(int Id, string Name, int ProvinceId);

public sealed record ThaiSubDistrict(int Id, string Name, string PostalCode, int DistrictId);
