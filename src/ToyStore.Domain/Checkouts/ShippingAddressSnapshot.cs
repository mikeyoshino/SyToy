namespace ToyStore.Domain.Checkouts;

public sealed record ShippingAddressSnapshot
{
    private ShippingAddressSnapshot()
    {
        RecipientName = null!;
        PhoneNumber = null!;
        AddressLine = null!;
        SubDistrict = null!;
        District = null!;
        Province = null!;
        PostalCode = null!;
    }

    private ShippingAddressSnapshot(
        string recipientName,
        string phoneNumber,
        string addressLine,
        string subDistrict,
        string district,
        string province,
        string postalCode)
    {
        RecipientName = recipientName;
        PhoneNumber = phoneNumber;
        AddressLine = addressLine;
        SubDistrict = subDistrict;
        District = district;
        Province = province;
        PostalCode = postalCode;
    }

    public string RecipientName { get; private set; }
    public string PhoneNumber { get; private set; }
    public string AddressLine { get; private set; }
    public string SubDistrict { get; private set; }
    public string District { get; private set; }
    public string Province { get; private set; }
    public string PostalCode { get; private set; }

    public static ShippingAddressSnapshot Create(
        string recipientName,
        string phoneNumber,
        string addressLine,
        string subDistrict,
        string district,
        string province,
        string postalCode)
    {
        return new(
            Prepare(recipientName, 200),
            Prepare(phoneNumber, 32),
            Prepare(addressLine, 500),
            Prepare(subDistrict, 200),
            Prepare(district, 200),
            Prepare(province, 200),
            PreparePostalCode(postalCode));
    }

    private static string Prepare(string value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Shipping address fields are required.", nameof(value));
        }

        var prepared = value.Trim();
        if (prepared.Length > maximumLength)
        {
            throw new ArgumentException("Shipping address field is too long.", nameof(value));
        }

        return prepared;
    }

    private static string PreparePostalCode(string value)
    {
        var prepared = Prepare(value, 5);
        if (prepared.Length != 5 || prepared.Any(character => character is < '0' or > '9'))
        {
            throw new ArgumentException("Thai postal code must contain exactly five digits.", nameof(value));
        }

        return prepared;
    }
}
