using ToyStore.Domain.Checkouts;

namespace ToyStore.Domain.Addresses;

public sealed class SavedAddress
{
    private SavedAddress()
    {
        CustomerId = null!;
        Label = null!;
        Address = null!;
    }

    private SavedAddress(Guid id, string customerId, string label,
        ShippingAddressSnapshot address, int provinceId, int districtId,
        int subDistrictId, bool isDefault, DateTimeOffset nowUtc)
    {
        Id = id;
        CustomerId = customerId.Trim();
        Label = PrepareLabel(label);
        Address = address;
        ProvinceId = RequirePositive(provinceId, nameof(provinceId));
        DistrictId = RequirePositive(districtId, nameof(districtId));
        SubDistrictId = RequirePositive(subDistrictId, nameof(subDistrictId));
        IsDefault = isDefault;
        CreatedAtUtc = RequireUtc(nowUtc);
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public string CustomerId { get; private set; }
    public string Label { get; private set; }
    public ShippingAddressSnapshot Address { get; private set; }
    public int ProvinceId { get; private set; }
    public int DistrictId { get; private set; }
    public int SubDistrictId { get; private set; }
    public bool IsDefault { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static SavedAddress Create(Guid id, string customerId, string label,
        ShippingAddressSnapshot address, int provinceId, int districtId,
        int subDistrictId, bool isDefault, DateTimeOffset nowUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Saved address id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(customerId))
            throw new ArgumentException("Customer id is required.", nameof(customerId));
        ArgumentNullException.ThrowIfNull(address);
        return new(id, customerId, label, address, provinceId, districtId,
            subDistrictId, isDefault, nowUtc);
    }

    public void MakeDefault(DateTimeOffset nowUtc)
    {
        IsDefault = true;
        UpdatedAtUtc = RequireUtc(nowUtc);
    }

    public void ClearDefault(DateTimeOffset nowUtc)
    {
        IsDefault = false;
        UpdatedAtUtc = RequireUtc(nowUtc);
    }

    private static string PrepareLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Saved address label is required.", nameof(value));
        var prepared = value.Trim();
        if (prepared.Length > 80)
            throw new ArgumentException("Saved address label is too long.", nameof(value));
        return prepared;
    }

    private static int RequirePositive(int value, string parameterName) => value > 0
        ? value
        : throw new ArgumentOutOfRangeException(parameterName);

    private static DateTimeOffset RequireUtc(DateTimeOffset value) => value.Offset == TimeSpan.Zero
        ? value
        : throw new ArgumentException("Saved address timestamps must be UTC.", nameof(value));
}
