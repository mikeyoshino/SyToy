using System.Text.RegularExpressions;

namespace ToyStore.Domain.Orders;

public enum ShippingCarrier { ThailandPost, Flash, Kerry, JAndT, Other }
public enum ShipmentRule { None, TrackingRequired, TrackingInvalid, OtherUrlRequired, OtherUrlInvalid, OtherUrlNotAllowed }

public sealed class Shipment
{
    private Shipment() { TrackingNumber = TrackingUrl = CreatedBy = null!; }

    private Shipment(Guid id, Guid orderId, Guid operationId, ShippingCarrier carrier,
        string trackingNumber, string trackingUrl, DateTimeOffset shippedAtUtc, string createdBy)
    {
        Id = id; OrderId = orderId; OperationId = operationId; Carrier = carrier;
        TrackingNumber = trackingNumber; TrackingUrl = trackingUrl;
        ShippedAtUtc = shippedAtUtc; CreatedBy = createdBy;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid OperationId { get; private set; }
    public ShippingCarrier Carrier { get; private set; }
    public string TrackingNumber { get; private set; }
    public string TrackingUrl { get; private set; }
    public DateTimeOffset ShippedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }

    public static Shipment Create(Guid id, Guid orderId, Guid operationId, ShippingCarrier carrier,
        string trackingNumber, string? otherTrackingUrl, DateTimeOffset shippedAtUtc, string createdBy)
    {
        if (id == Guid.Empty || orderId == Guid.Empty || operationId == Guid.Empty)
            throw new ArgumentException("Shipment identity is required.");
        if (shippedAtUtc.Offset != TimeSpan.Zero || string.IsNullOrWhiteSpace(createdBy))
            throw new ArgumentException("Shipment audit identity and UTC timestamp are required.");
        var trimmed = trackingNumber?.Trim() ?? string.Empty;
        var normalized = carrier == ShippingCarrier.Other ? trimmed : trimmed.ToUpperInvariant();
        var rule = Validate(carrier, normalized, otherTrackingUrl, out var url);
        if (rule != ShipmentRule.None) throw new ShipmentRuleException(rule);
        return new Shipment(id, orderId, operationId, carrier, normalized, url!, shippedAtUtc, createdBy.Trim());
    }

    public static ShipmentRule Validate(ShippingCarrier carrier, string? trackingNumber,
        string? otherTrackingUrl, out string? trackingUrl)
    {
        trackingUrl = null;
        var tracking = trackingNumber?.Trim() ?? string.Empty;
        if (tracking.Length == 0) return ShipmentRule.TrackingRequired;
        if (tracking.Length > 100) return ShipmentRule.TrackingInvalid;
        var compact = tracking.ToUpperInvariant();
        var valid = carrier switch
        {
            ShippingCarrier.ThailandPost => Regex.IsMatch(compact, "^[A-Z]{2}[0-9]{9}TH$"),
            ShippingCarrier.Flash or ShippingCarrier.Kerry or ShippingCarrier.JAndT =>
                Regex.IsMatch(compact, "^[A-Z0-9-]{8,40}$"),
            ShippingCarrier.Other => Regex.IsMatch(tracking, "^[A-Za-z0-9][A-Za-z0-9._/-]{1,99}$"),
            _ => false,
        };
        if (!valid) return ShipmentRule.TrackingInvalid;

        if (carrier == ShippingCarrier.Other)
        {
            if (string.IsNullOrWhiteSpace(otherTrackingUrl)) return ShipmentRule.OtherUrlRequired;
            if (!Uri.TryCreate(otherTrackingUrl.Trim(), UriKind.Absolute, out var other)
                || other.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(other.UserInfo)
                || other.Host.Length == 0 || otherTrackingUrl.Length > 500)
                return ShipmentRule.OtherUrlInvalid;
            trackingUrl = other.AbsoluteUri;
            return ShipmentRule.None;
        }
        if (!string.IsNullOrWhiteSpace(otherTrackingUrl)) return ShipmentRule.OtherUrlNotAllowed;
        var escaped = Uri.EscapeDataString(compact);
        trackingUrl = carrier switch
        {
            ShippingCarrier.ThailandPost => $"https://track.thailandpost.co.th/?trackNumber={escaped}",
            ShippingCarrier.Flash => $"https://www.flashexpress.com/fle/tracking?se={escaped}",
            ShippingCarrier.Kerry => $"https://th.kex-express.com/th/track/?track={escaped}",
            ShippingCarrier.JAndT => $"https://www.jtexpress.co.th/service/track?waybill={escaped}",
            _ => null,
        };
        return trackingUrl is null ? ShipmentRule.TrackingInvalid : ShipmentRule.None;
    }
}

public sealed class ShipmentRuleException(ShipmentRule rule) : InvalidOperationException($"Shipment rule failed: {rule}.")
{
    public ShipmentRule Rule { get; } = rule;
}

public sealed class OrderAuditEvent
{
    private OrderAuditEvent() { Action = ActorId = Detail = null!; }
    private OrderAuditEvent(Guid id, Guid orderId, Guid operationId, string action,
        string actorId, string detail, DateTimeOffset occurredAtUtc)
    {
        Id = id; OrderId = orderId; OperationId = operationId; Action = action;
        ActorId = actorId; Detail = detail; OccurredAtUtc = occurredAtUtc;
    }
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid OperationId { get; private set; }
    public string Action { get; private set; }
    public string ActorId { get; private set; }
    public string Detail { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }

    public static OrderAuditEvent ShipmentCreated(Guid id, Guid orderId, Guid operationId,
        string actorId, ShippingCarrier carrier, string trackingNumber, DateTimeOffset occurredAtUtc) =>
        new(id, orderId, operationId, "ShipmentCreated", actorId.Trim(),
            $"{carrier}:{trackingNumber}", occurredAtUtc);
}
