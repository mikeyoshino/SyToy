using ToyStore.Domain.PreOrders;
using ToyStore.Domain.Products;

namespace ToyStore.UnitTests.Domain.PreOrders;

public sealed class PreOrderCapacityTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CloseAtUtc =
        new(2026, 12, 31, 16, 59, 59, TimeSpan.Zero);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void CreateRejectsCapacityAtOrAfterClose(int minutesAfterClose)
    {
        var offer = PreOrderOffer.Create(Money.Create(1000), Money.Create(200), new DateOnly(2026, 12, 31), EstimatedArrival.Create(1, 2027), 10, 2, CreatedAtUtc);
        var createdAt = CloseAtUtc.AddMinutes(minutesAfterClose);
        var exception = Assert.Throws<PreOrderCapacityRuleException>(() => PreOrderCapacity.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), offer, "เปิด", "product:1", createdAt, "admin"));
        Assert.Equal(PreOrderCapacityRule.PreOrderClosed, exception.Rule);
    }

    [Fact]
    public void CreateUsesOfferCapacityAndCloseAndRecordsInitialMovement()
    {
        var creation = CreateCapacity(totalCapacity: 10);

        Assert.Equal(10, creation.Capacity.TotalCapacity);
        Assert.Equal(10, creation.Capacity.RemainingQuantity);
        Assert.Equal(CloseAtUtc, creation.Capacity.CloseAtUtc);
        Assert.Equal(1, creation.Capacity.Version);
        Assert.Equal(PreOrderCapacityMovementType.InitialCapacity, creation.Movement.Type);
        Assert.Equal(10, creation.Movement.AvailableQuantityDelta);
        Assert.Equal(10, creation.Movement.ResultingRemainingQuantity);
    }

    [Fact]
    public void ReserveMovesRemainingCapacityIntoHoldAndRecordsMovement()
    {
        var capacity = CreateCapacity(totalCapacity: 3).Capacity;

        var creation = capacity.Reserve(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer-1",
            quantity: 2,
            CreatedAtUtc.AddMinutes(1),
            CreatedAtUtc.AddMinutes(33),
            Guid.NewGuid(),
            "เริ่มชำระมัดจำ",
            "checkout:1",
            expectedVersion: 1,
            "customer-1");

        Assert.Equal(1, capacity.RemainingQuantity);
        Assert.Equal(2, capacity.HeldQuantity);
        Assert.Equal(0, capacity.CommittedQuantity);
        Assert.Equal(0, capacity.RetiredQuantity);
        Assert.Equal(2, capacity.Version);
        Assert.Equal(PreOrderCapacityReservationStatus.Active, creation.Reservation.Status);
        Assert.Equal(PreOrderCapacityMovementType.Reserved, creation.Movement.Type);
        Assert.Equal(-2, creation.Movement.AvailableQuantityDelta);
        Assert.Equal(1, creation.Movement.ResultingRemainingQuantity);
    }

    [Theory]
    [InlineData(3, PreOrderCapacityRule.InsufficientRemainingCapacity)]
    [InlineData(0, PreOrderCapacityRule.QuantityMustBePositive)]
    public void ReserveRejectsInvalidQuantityWithoutChangingCounters(
        int quantity,
        PreOrderCapacityRule expectedRule)
    {
        var capacity = CreateCapacity(totalCapacity: 2).Capacity;

        var exception = Assert.Throws<PreOrderCapacityRuleException>(() => capacity.Reserve(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer-1",
            quantity,
            CreatedAtUtc.AddMinutes(1),
            CreatedAtUtc.AddMinutes(33),
            Guid.NewGuid(),
            "เริ่มชำระมัดจำ",
            "checkout:1",
            expectedVersion: 1,
            "customer-1"));

        Assert.Equal(expectedRule, exception.Rule);
        Assert.Equal(2, capacity.RemainingQuantity);
        Assert.Equal(0, capacity.HeldQuantity);
        Assert.Equal(1, capacity.Version);
    }

    [Fact]
    public void ReserveAtExactCloseIsRejected()
    {
        var capacity = CreateCapacity().Capacity;

        var exception = Assert.Throws<PreOrderCapacityRuleException>(() => capacity.Reserve(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer-1",
            1,
            CloseAtUtc,
            CloseAtUtc.AddMinutes(32),
            Guid.NewGuid(),
            "เริ่มชำระมัดจำ",
            "checkout:close",
            expectedVersion: 1,
            "customer-1"));

        Assert.Equal(PreOrderCapacityRule.PreOrderClosed, exception.Rule);
    }

    [Fact]
    public void ReserveRequiresCurrentVersionAndUtcExpiryAfterReservation()
    {
        var capacity = CreateCapacity().Capacity;

        var stale = Assert.Throws<PreOrderCapacityRuleException>(() => capacity.Reserve(
            Guid.NewGuid(), Guid.NewGuid(), "customer-1", 1,
            CreatedAtUtc.AddMinutes(1), CreatedAtUtc.AddMinutes(33), Guid.NewGuid(),
            "เริ่มชำระมัดจำ", "checkout:1", expectedVersion: 99, "customer-1"));
        var invalidExpiry = Assert.Throws<PreOrderCapacityRuleException>(() => capacity.Reserve(
            Guid.NewGuid(), Guid.NewGuid(), "customer-1", 1,
            CreatedAtUtc.AddMinutes(1), CreatedAtUtc.AddMinutes(1), Guid.NewGuid(),
            "เริ่มชำระมัดจำ", "checkout:2", expectedVersion: 1, "customer-1"));
        var nonUtc = Assert.Throws<PreOrderCapacityRuleException>(() => capacity.Reserve(
            Guid.NewGuid(), Guid.NewGuid(), "customer-1", 1,
            new DateTimeOffset(2026, 11, 1, 7, 1, 0, TimeSpan.FromHours(7)),
            CreatedAtUtc.AddMinutes(33), Guid.NewGuid(),
            "เริ่มชำระมัดจำ", "checkout:3", expectedVersion: 1, "customer-1"));

        Assert.Equal(PreOrderCapacityRule.ConcurrencyVersionMismatch, stale.Rule);
        Assert.Equal(PreOrderCapacityRule.ReservationExpiryInvalid, invalidExpiry.Rule);
        Assert.Equal(PreOrderCapacityRule.AuditInstantMustBeUtc, nonUtc.Rule);
    }

    [Fact]
    public void ReleaseRestoresHeldCapacityAndExactRetryIsNoOp()
    {
        var capacity = CreateCapacity(totalCapacity: 2).Capacity;
        var reservation = ReserveOne(capacity).Reservation;
        var movementId = Guid.NewGuid();

        var first = capacity.ReleaseReservation(
            reservation,
            movementId,
            "Stripe ยืนยัน session หมดอายุ",
            "stripe:cs_1",
            expectedVersion: 2,
            CreatedAtUtc.AddMinutes(34),
            "system");
        var retry = capacity.ReleaseReservation(
            reservation,
            movementId,
            "retry ไม่ควรเปลี่ยนผล",
            "stripe:cs_1",
            expectedVersion: 2,
            CreatedAtUtc.AddMinutes(35),
            "system");

        Assert.True(first.Changed);
        Assert.False(retry.Changed);
        Assert.Equal(2, capacity.RemainingQuantity);
        Assert.Equal(0, capacity.HeldQuantity);
        Assert.Equal(3, capacity.Version);
        Assert.Equal(PreOrderCapacityMovementType.Released, first.Movement?.Type);
        Assert.Equal(1, first.Movement?.AvailableQuantityDelta);
    }

    [Fact]
    public void ExpireRequiresBoundaryAndExactRetryIsNoOp()
    {
        var capacity = CreateCapacity().Capacity;
        var reservation = ReserveOne(capacity).Reservation;
        var movementId = Guid.NewGuid();

        var early = Assert.Throws<PreOrderCapacityRuleException>(() => capacity.ExpireReservation(
            reservation, movementId, "หมดเวลา", "maintenance:1", capacity.Version,
            reservation.ExpiresAtUtc.AddTicks(-1), "system"));
        var first = capacity.ExpireReservation(
            reservation, movementId, "หมดเวลา", "maintenance:1", capacity.Version,
            reservation.ExpiresAtUtc, "system");
        var retry = capacity.ExpireReservation(
            reservation, movementId, "หมดเวลา retry", "maintenance:1", expectedVersion: 2,
            reservation.ExpiresAtUtc.AddMinutes(1), "system");

        Assert.Equal(PreOrderCapacityRule.ReservationExpireTooEarly, early.Rule);
        Assert.True(first.Changed);
        Assert.False(retry.Changed);
        Assert.Equal(PreOrderCapacityReservationStatus.Expired, reservation.Status);
    }

    [Fact]
    public void ConsumeMovesHoldIntoCommittedAndExactRetryIsNoOp()
    {
        var capacity = CreateCapacity(totalCapacity: 2).Capacity;
        var reservation = ReserveOne(capacity).Reservation;
        var movementId = Guid.NewGuid();

        var first = capacity.ConsumeReservation(
            reservation, movementId, "ยืนยันมัดจำแล้ว", "payment:pi_1",
            expectedVersion: 2, CreatedAtUtc.AddMinutes(5), "stripe-webhook");
        var retry = capacity.ConsumeReservation(
            reservation, movementId, "webhook retry", "payment:pi_1",
            expectedVersion: 2, CreatedAtUtc.AddMinutes(6), "stripe-webhook");

        Assert.True(first.Changed);
        Assert.False(retry.Changed);
        Assert.Equal(1, capacity.RemainingQuantity);
        Assert.Equal(0, capacity.HeldQuantity);
        Assert.Equal(1, capacity.CommittedQuantity);
        Assert.Equal(PreOrderCapacityReservationStatus.Consumed, reservation.Status);
        Assert.Equal(PreOrderCapacityMovementType.ReservationConsumed, first.Movement?.Type);
        Assert.Equal(0, first.Movement?.AvailableQuantityDelta);
    }

    [Fact]
    public void CustomerCancellationBeforeCloseReopensCapacityAndForfeitsDeposit()
    {
        var (capacity, reservation) = CreateConsumedReservation();

        var result = capacity.CancelReservation(
            reservation,
            Guid.NewGuid(),
            PreOrderCancellationKind.Customer,
            "ลูกค้ายกเลิก",
            "order:PO-1:cancel",
            expectedVersion: 3,
            CreatedAtUtc.AddMinutes(10),
            "customer-1");

        Assert.True(result.Changed);
        Assert.Equal(2, capacity.RemainingQuantity);
        Assert.Equal(0, capacity.CommittedQuantity);
        Assert.Equal(0, capacity.RetiredQuantity);
        Assert.Equal(PreOrderDepositDisposition.Forfeited, reservation.DepositDisposition);
        Assert.Equal(PreOrderCapacityMovementType.CancellationReopened, result.Movement?.Type);
        Assert.Equal(1, result.Movement?.AvailableQuantityDelta);
    }

    [Theory]
    [InlineData(PreOrderCancellationKind.Customer, PreOrderDepositDisposition.Forfeited)]
    [InlineData(PreOrderCancellationKind.BalanceOverdue, PreOrderDepositDisposition.Forfeited)]
    [InlineData(PreOrderCancellationKind.AdminOrSupplier, PreOrderDepositDisposition.RefundRequired)]
    public void CancellationAtOrAfterCloseRetiresCapacityAndUsesApprovedDepositPolicy(
        PreOrderCancellationKind cancellationKind,
        PreOrderDepositDisposition expectedDisposition)
    {
        var (capacity, reservation) = CreateConsumedReservation();
        var movementId = Guid.NewGuid();

        var first = capacity.CancelReservation(
            reservation, movementId, cancellationKind, "ยกเลิกหลังปิดรอบ", "order:PO-1:cancel",
            expectedVersion: 3, CloseAtUtc, "admin@example.com");
        var retry = capacity.CancelReservation(
            reservation, movementId, cancellationKind, "retry", "order:PO-1:cancel",
            expectedVersion: 3, CloseAtUtc.AddMinutes(1), "admin@example.com");

        Assert.True(first.Changed);
        Assert.False(retry.Changed);
        Assert.Equal(1, capacity.RemainingQuantity);
        Assert.Equal(0, capacity.CommittedQuantity);
        Assert.Equal(1, capacity.RetiredQuantity);
        Assert.Equal(expectedDisposition, reservation.DepositDisposition);
        Assert.Equal(PreOrderCapacityMovementType.CancellationRetired, first.Movement?.Type);
        Assert.Equal(0, first.Movement?.AvailableQuantityDelta);
        Assert.Equal(2, capacity.TotalCapacity);
    }

    [Fact]
    public void ConflictingDuplicateOrCancellationBeforeDepositIsRejected()
    {
        var capacity = CreateCapacity().Capacity;
        var reservation = ReserveOne(capacity).Reservation;
        var releaseMovementId = Guid.NewGuid();
        capacity.ReleaseReservation(
            reservation, releaseMovementId, "release", "stripe:1", capacity.Version,
            CreatedAtUtc.AddMinutes(4), "system");

        var conflictingRetry = Assert.Throws<PreOrderCapacityRuleException>(() =>
            capacity.ReleaseReservation(
                reservation, Guid.NewGuid(), "release", "stripe:other", expectedVersion: 2,
                CreatedAtUtc.AddMinutes(5), "system"));
        var cancellation = Assert.Throws<PreOrderCapacityRuleException>(() =>
            capacity.CancelReservation(
                reservation, Guid.NewGuid(), PreOrderCancellationKind.Customer,
                "cancel", "order:1", capacity.Version,
                CreatedAtUtc.AddMinutes(6), "customer-1"));

        Assert.Equal(PreOrderCapacityRule.ReservationEvidenceConflict, conflictingRetry.Rule);
        Assert.Equal(PreOrderCapacityRule.ReservationTransitionInvalid, cancellation.Rule);
    }

    private static PreOrderCapacityCreation CreateCapacity(int totalCapacity = 10)
    {
        var offer = PreOrderOffer.Create(
            Money.Create(1000),
            Money.Create(200),
            new DateOnly(2026, 12, 31),
            EstimatedArrival.Create(1, 2027),
            totalCapacity,
            Math.Min(2, totalCapacity),
            CreatedAtUtc);

        return PreOrderCapacity.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            offer,
            "เปิด capacity",
            "product:create",
            CreatedAtUtc,
            "admin@example.com");
    }

    private static PreOrderCapacityReservationCreation ReserveOne(PreOrderCapacity capacity) =>
        capacity.Reserve(
            Guid.NewGuid(), Guid.NewGuid(), "customer-1", 1,
            CreatedAtUtc.AddMinutes(1), CreatedAtUtc.AddMinutes(33), Guid.NewGuid(),
            "เริ่มชำระมัดจำ", "checkout:1", capacity.Version, "customer-1");

    private static (PreOrderCapacity Capacity, PreOrderCapacityReservation Reservation)
        CreateConsumedReservation()
    {
        var capacity = CreateCapacity(totalCapacity: 2).Capacity;
        var reservation = ReserveOne(capacity).Reservation;
        capacity.ConsumeReservation(
            reservation, Guid.NewGuid(), "ยืนยันมัดจำแล้ว", "payment:pi_1",
            capacity.Version, CreatedAtUtc.AddMinutes(5), "stripe-webhook");
        return (capacity, reservation);
    }
}
