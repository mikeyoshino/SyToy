using ToyStore.Domain.Orders;

namespace ToyStore.UnitTests.Domain.Orders;

public sealed class ShipmentTests
{
    [Fact]
    public void CarrierTrackingBuildsSafeOfficialUrlAndOtherRequiresHttps()
    {
        var thailandPost = Shipment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            ShippingCarrier.ThailandPost, "EF123456789TH", null, DateTimeOffset.UtcNow, "admin");
        Assert.Equal("https://track.thailandpost.co.th/?trackNumber=EF123456789TH", thailandPost.TrackingUrl);

        var invalid = Assert.Throws<ShipmentRuleException>(() => Shipment.Create(Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), ShippingCarrier.Other, "TRACK-12345",
            "http://example.test/track", DateTimeOffset.UtcNow, "admin"));
        Assert.Equal(ShipmentRule.OtherUrlInvalid, invalid.Rule);

        var other = Shipment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            ShippingCarrier.Other, "TRACK-12345", "https://carrier.example/track/TRACK-12345",
            DateTimeOffset.UtcNow, "admin");
        Assert.Equal("https", new Uri(other.TrackingUrl).Scheme);
    }

    [Theory]
    [InlineData(ShippingCarrier.ThailandPost, "123")]
    [InlineData(ShippingCarrier.Flash, "bad space")]
    [InlineData(ShippingCarrier.Kerry, "short")]
    [InlineData(ShippingCarrier.JAndT, "")]
    public void InvalidCarrierTrackingIsRejected(ShippingCarrier carrier, string tracking)
    {
        Assert.NotEqual(ShipmentRule.None, Shipment.Validate(carrier, tracking, null, out _));
    }
}
