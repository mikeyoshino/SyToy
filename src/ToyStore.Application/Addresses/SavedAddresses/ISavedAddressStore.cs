using ToyStore.Application.Common.Models;
using ToyStore.Domain.Addresses;

namespace ToyStore.Application.Addresses.SavedAddresses;

public interface ISavedAddressStore
{
    Task<IReadOnlyList<SavedAddressView>> ListAsync(string customerId, CancellationToken cancellationToken);
    Task<Result<SavedAddressView>> CreateAsync(SavedAddress address, CancellationToken cancellationToken);
    Task<Result> DeleteAsync(string customerId, Guid addressId, DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
    Task<Result> SetDefaultAsync(string customerId, Guid addressId, DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}

public sealed record SavedAddressView(
    Guid Id,
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
    bool IsDefault);

public static class SavedAddressErrors
{
    public static readonly Error LimitReached = new("SavedAddress.LimitReached",
        "บันทึกที่อยู่ได้สูงสุด 5 รายการ กรุณาลบที่อยู่เดิมก่อน", ErrorType.Conflict);
    public static readonly Error NotFound = new("SavedAddress.NotFound",
        "ไม่พบที่อยู่ที่บันทึกไว้", ErrorType.NotFound);
    public static readonly Error AddressInvalid = new("SavedAddress.AddressInvalid",
        "จังหวัด อำเภอ ตำบล หรือรหัสไปรษณีย์ไม่สัมพันธ์กัน", ErrorType.Validation);
}
