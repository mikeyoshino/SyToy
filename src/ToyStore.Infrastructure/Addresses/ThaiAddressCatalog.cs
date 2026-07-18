using System.Collections.Frozen;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ToyStore.Application.Addresses;

namespace ToyStore.Infrastructure.Addresses;

internal sealed class ThaiAddressCatalog : IThaiAddressCatalog
{
    private const string ResourceName = "ToyStore.Infrastructure.Addresses.Data.thai-addresses.326c2ebe.json";
    private const int ExpectedProvinceCount = 77;
    private const int ExpectedDistrictCount = 930;
    private const int ExpectedSubDistrictCount = 7452;
    private const int ExpectedEmptyDistrictCount = 2;

    private readonly FrozenDictionary<int, IReadOnlyList<ThaiDistrict>> districtsByProvince;
    private readonly FrozenDictionary<int, IReadOnlyList<ThaiSubDistrict>> subDistrictsByDistrict;
    private readonly FrozenSet<AddressKey> validAddresses;

    private ThaiAddressCatalog(
        IReadOnlyList<ThaiProvince> provinces,
        FrozenDictionary<int, IReadOnlyList<ThaiDistrict>> districtsByProvince,
        FrozenDictionary<int, IReadOnlyList<ThaiSubDistrict>> subDistrictsByDistrict,
        FrozenSet<AddressKey> validAddresses)
    {
        Provinces = provinces;
        this.districtsByProvince = districtsByProvince;
        this.subDistrictsByDistrict = subDistrictsByDistrict;
        this.validAddresses = validAddresses;
    }

    public IReadOnlyList<ThaiProvince> Provinces { get; }

    public IReadOnlyList<ThaiDistrict> GetDistricts(int provinceId) =>
        districtsByProvince.GetValueOrDefault(provinceId, []);

    public IReadOnlyList<ThaiSubDistrict> GetSubDistricts(int districtId) =>
        subDistrictsByDistrict.GetValueOrDefault(districtId, []);

    public bool IsValid(string province, string district, string subDistrict, string postalCode) =>
        validAddresses.Contains(new AddressKey(
            Normalize(province), Normalize(district), Normalize(subDistrict), Normalize(postalCode)));

    public static ThaiAddressCatalog Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"ไม่พบชุดข้อมูลที่อยู่ไทย embedded resource: {ResourceName}");
        var source = JsonSerializer.Deserialize<SourceProvince[]>(stream)
            ?? throw new InvalidOperationException("อ่านชุดข้อมูลที่อยู่ไทยไม่สำเร็จ");

        var provinceIds = new HashSet<int>();
        var districtIds = new HashSet<int>();
        var subDistrictIds = new HashSet<int>();
        var provinces = new List<ThaiProvince>(source.Length);
        var districtGroups = new Dictionary<int, IReadOnlyList<ThaiDistrict>>();
        var subDistrictGroups = new Dictionary<int, IReadOnlyList<ThaiSubDistrict>>();
        var addressKeys = new List<AddressKey>(ExpectedSubDistrictCount);
        var districtCount = 0;
        var subDistrictCount = 0;
        var emptyDistrictCount = 0;

        foreach (var province in source.OrderBy(item => item.NameTh, StringComparer.Ordinal))
        {
            Require(provinceIds.Add(province.Id) && !string.IsNullOrWhiteSpace(province.NameTh), "ข้อมูลจังหวัดซ้ำหรือไม่สมบูรณ์");
            provinces.Add(new ThaiProvince(province.Id, province.NameTh.Trim()));
            var districts = new List<ThaiDistrict>(province.Districts.Length);

            foreach (var district in province.Districts.OrderBy(item => item.NameTh, StringComparer.Ordinal))
            {
                Require(district.ProvinceId == province.Id && districtIds.Add(district.Id)
                    && !string.IsNullOrWhiteSpace(district.NameTh), "ความสัมพันธ์อำเภอ/เขตไม่ถูกต้อง");
                districtCount++;
                var subDistricts = new List<ThaiSubDistrict>(district.SubDistricts.Length);

                foreach (var subDistrict in district.SubDistricts.OrderBy(item => item.NameTh, StringComparer.Ordinal))
                {
                    var postalCode = subDistrict.ZipCode.ToString("00000", CultureInfo.InvariantCulture);
                    Require(subDistrict.DistrictId == district.Id && subDistrictIds.Add(subDistrict.Id)
                        && !string.IsNullOrWhiteSpace(subDistrict.NameTh) && postalCode.Length == 5,
                        "ความสัมพันธ์ตำบล/แขวงหรือรหัสไปรษณีย์ไม่ถูกต้อง");
                    subDistricts.Add(new ThaiSubDistrict(
                        subDistrict.Id, subDistrict.NameTh.Trim(), postalCode, district.Id));
                    addressKeys.Add(new AddressKey(
                        Normalize(province.NameTh), Normalize(district.NameTh),
                        Normalize(subDistrict.NameTh), postalCode));
                    subDistrictCount++;
                }

                if (subDistricts.Count == 0)
                {
                    emptyDistrictCount++;
                    continue;
                }

                districts.Add(new ThaiDistrict(district.Id, district.NameTh.Trim(), province.Id));
                subDistrictGroups.Add(district.Id, subDistricts.AsReadOnly());
            }

            Require(districts.Count != 0, "พบจังหวัดที่ไม่มีอำเภอ/เขต");
            districtGroups.Add(province.Id, districts.AsReadOnly());
        }

        Require(provinces.Count == ExpectedProvinceCount
            && districtCount == ExpectedDistrictCount
            && subDistrictCount == ExpectedSubDistrictCount
            && emptyDistrictCount == ExpectedEmptyDistrictCount,
            $"จำนวนข้อมูลที่อยู่ไทยไม่ตรงกับชุดข้อมูลที่ pin: {provinces.Count}/{districtCount}/{subDistrictCount}");

        return new ThaiAddressCatalog(
            provinces.AsReadOnly(),
            districtGroups.ToFrozenDictionary(),
            subDistrictGroups.ToFrozenDictionary(),
            addressKeys.ToFrozenSet());
    }

    private static string Normalize(string value) => value.Trim();

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private readonly record struct AddressKey(string Province, string District, string SubDistrict, string PostalCode);

    private sealed record SourceProvince(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name_th")] string NameTh,
        [property: JsonPropertyName("districts")] SourceDistrict[] Districts);

    private sealed record SourceDistrict(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name_th")] string NameTh,
        [property: JsonPropertyName("province_id")] int ProvinceId,
        [property: JsonPropertyName("sub_districts")] SourceSubDistrict[] SubDistricts);

    private sealed record SourceSubDistrict(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("zip_code")] int ZipCode,
        [property: JsonPropertyName("name_th")] string NameTh,
        [property: JsonPropertyName("district_id")] int DistrictId);
}
