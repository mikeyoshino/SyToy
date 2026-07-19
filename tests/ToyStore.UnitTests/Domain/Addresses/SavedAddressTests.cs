using ToyStore.Domain.Addresses;
using ToyStore.Domain.Checkouts;

namespace ToyStore.UnitTests.Domain.Addresses;

public sealed class SavedAddressTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateTrimsIdentityAndStoresThaiAddressRelationshipIds()
    {
        var saved = SavedAddress.Create(Guid.NewGuid(), " customer ", " บ้าน ", Address(),
            1, 2, 3, false, Now);

        Assert.Equal("customer", saved.CustomerId);
        Assert.Equal("บ้าน", saved.Label);
        Assert.Equal((1, 2, 3), (saved.ProvinceId, saved.DistrictId, saved.SubDistrictId));
        Assert.False(saved.IsDefault);
        Assert.Equal(Now, saved.CreatedAtUtc);
    }

    [Fact]
    public void DefaultTransitionsRequireUtcAndUpdateTimestamp()
    {
        var saved = SavedAddress.Create(Guid.NewGuid(), "customer", "บ้าน", Address(),
            1, 2, 3, false, Now);
        saved.MakeDefault(Now.AddMinutes(1));
        Assert.True(saved.IsDefault);
        Assert.Equal(Now.AddMinutes(1), saved.UpdatedAtUtc);

        saved.ClearDefault(Now.AddMinutes(2));
        Assert.False(saved.IsDefault);
        Assert.Throws<ArgumentException>(() => saved.MakeDefault(Now.ToOffset(TimeSpan.FromHours(7))));
    }

    [Fact]
    public void CreateRejectsMissingOwnershipLabelAndLocationIds()
    {
        Assert.Throws<ArgumentException>(() => SavedAddress.Create(Guid.NewGuid(), "", "บ้าน",
            Address(), 1, 2, 3, false, Now));
        Assert.Throws<ArgumentException>(() => SavedAddress.Create(Guid.NewGuid(), "customer", " ",
            Address(), 1, 2, 3, false, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => SavedAddress.Create(Guid.NewGuid(), "customer",
            "บ้าน", Address(), 0, 2, 3, false, Now));
    }

    private static ShippingAddressSnapshot Address() => ShippingAddressSnapshot.Create(
        "สมชาย ใจดี", "0812345678", "99 ถนนสุขุมวิท", "คลองตัน", "คลองเตย",
        "กรุงเทพมหานคร", "10110");
}
