namespace ToyStore.Web.Components.Forms;

public sealed class ShippingAddressFormModel
{
    public string? RecipientName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AddressLine { get; set; }
    public int? ProvinceId { get; set; }
    public int? DistrictId { get; set; }
    public int? SubDistrictId { get; set; }
    public string? Province { get; set; }
    public string? District { get; set; }
    public string? SubDistrict { get; set; }
    public string? PostalCode { get; set; }
}
