using System.Reflection;
using ToyStore.Domain.Inventory;

namespace ToyStore.UnitTests.Domain.Inventory;

public sealed class StockReservationTests
{
    private static readonly Guid InventoryId = Guid.Parse("72000000-0000-0000-0000-000000000001");
    private static readonly Guid ProductId = Guid.Parse("72000000-0000-0000-0000-000000000002");
    private static readonly Guid ReservationId = Guid.Parse("72000000-0000-0000-0000-000000000003");
    private static readonly Guid CheckoutAttemptId = Guid.Parse("72000000-0000-0000-0000-000000000004");
    private static readonly DateTimeOffset StartedAtUtc =
        new(2026, 7, 17, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ExpiresAtUtc = StartedAtUtc.AddMinutes(32);

    [Fact]
    public void ReservationShapeIsImmutableAndExposesOnlyApprovedStatuses()
    {
        Assert.Empty(typeof(StockReservation).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.All(typeof(StockReservation).GetProperties(), property =>
            Assert.False(property.SetMethod?.IsPublic ?? false));
        Assert.Equal(
            [
                StockReservationStatus.Active,
                StockReservationStatus.Released,
                StockReservationStatus.Expired,
                StockReservationStatus.Consumed,
            ],
            Enum.GetValues<StockReservationStatus>());
    }

    [Fact]
    public void ReserveExactQuantityCreatesFailClosedHoldAndOneVersionAdvance()
    {
        var item = Item(onHand: 5);
        var reservation = Reserve(item, quantity: 5);

        Assert.Equal(ReservationId, reservation.Id);
        Assert.Equal(InventoryId, reservation.InventoryItemId);
        Assert.Equal(ProductId, reservation.ProductId);
        Assert.Equal(CheckoutAttemptId, reservation.CheckoutAttemptId);
        Assert.Equal(5, reservation.Quantity);
        Assert.Equal(StartedAtUtc, reservation.ReservedAtUtc);
        Assert.Equal(ExpiresAtUtc, reservation.ExpiresAtUtc);
        Assert.Equal(StockReservationStatus.Active, reservation.Status);
        Assert.Equal(5, item.HeldQuantity);
        Assert.Equal(0, item.ReservableQuantity);
        Assert.Equal(2, item.Version);
        Assert.Equal(StartedAtUtc, item.UpdatedAtUtc);

        AssertUnchanged(item, InventoryRule.InsufficientReservableQuantity, () => Reserve(
            item,
            reservationId: Guid.NewGuid(),
            checkoutAttemptId: Guid.NewGuid(),
            quantity: 1,
            expectedVersion: 2));
    }

    [Fact]
    public void ReserveRejectsInvalidShapeExpiryAuditAndVersionWithoutMutation()
    {
        var item = Item();
        AssertUnchanged(item, InventoryRule.ReservationIdentityRequired, () => Reserve(item, reservationId: Guid.Empty));
        AssertUnchanged(item, InventoryRule.CheckoutAttemptIdentityRequired, () => Reserve(item, checkoutAttemptId: Guid.Empty));
        AssertUnchanged(item, InventoryRule.QuantityMustBePositive, () => Reserve(item, quantity: 0));
        AssertUnchanged(item, InventoryRule.ReservationExpiryInvalid, () => Reserve(item, expiresAtUtc: StartedAtUtc));
        AssertUnchanged(
            item,
            InventoryRule.AuditInstantMustBeUtc,
            () => Reserve(item, reservedAtUtc: StartedAtUtc.ToOffset(TimeSpan.FromHours(7))));
        AssertUnchanged(item, InventoryRule.ConcurrencyVersionMismatch, () => Reserve(item, expectedVersion: 2));
    }

    [Fact]
    public void AvailabilityExcludesExactExpiryButReservableRemainsFailClosedUntilTransition()
    {
        var item = Item(onHand: 10);
        var reservation = Reserve(item, quantity: 4);

        var before = InventoryAvailability.Calculate(item, [reservation], ExpiresAtUtc.AddTicks(-1));
        var exact = InventoryAvailability.Calculate(item, [reservation], ExpiresAtUtc);

        Assert.Equal(4, before.EffectiveActiveReservedQuantity);
        Assert.Equal(6, before.AvailableQuantity);
        Assert.Equal(0, exact.EffectiveActiveReservedQuantity);
        Assert.Equal(10, exact.AvailableQuantity);
        Assert.Equal(4, item.HeldQuantity);
        Assert.Equal(6, item.ReservableQuantity);
    }

    [Fact]
    public void AvailabilityRejectsWrongRowsDuplicatesAndImpossibleCompleteSnapshot()
    {
        var item = Item(onHand: 3);
        var reservation = Reserve(item, quantity: 2);
        var wrongItem = Item(Guid.NewGuid(), ProductId, onHand: 2);
        var wrongReservation = Reserve(wrongItem, reservationId: Guid.NewGuid(), quantity: 1);

        AssertRule(
            InventoryRule.ReservationIdentityDuplicate,
            () => InventoryAvailability.Calculate(item, [reservation, reservation], StartedAtUtc));
        AssertRule(
            InventoryRule.AvailabilitySnapshotInvalid,
            () => InventoryAvailability.Calculate(item, [], StartedAtUtc));

        var second = Reserve(
            item,
            reservationId: Guid.NewGuid(),
            checkoutAttemptId: Guid.NewGuid(),
            quantity: 1,
            expectedVersion: 2);
        AssertRule(
            InventoryRule.AvailabilitySnapshotInvalid,
            () => InventoryAvailability.Calculate(item, [reservation], StartedAtUtc));
        Assert.Equal(
            3,
            InventoryAvailability.Calculate(item, [reservation, second], StartedAtUtc)
                .EffectiveActiveReservedQuantity);
        AssertRule(
            InventoryRule.ReservationInventoryMismatch,
            () => InventoryAvailability.Calculate(item, [wrongReservation], StartedAtUtc));

        var wrongProductItem = Item(InventoryId, Guid.NewGuid(), onHand: 2);
        var wrongProductReservation = Reserve(
            wrongProductItem,
            reservationId: Guid.NewGuid(),
            quantity: 1);
        AssertRule(
            InventoryRule.ReservationProductMismatch,
            () => InventoryAvailability.Calculate(item, [wrongProductReservation], StartedAtUtc));
    }

    [Fact]
    public void UnexpiredReleaseIncreasesAvailableAndReservableAndRetryRequiresSameEvidence()
    {
        var item = Item(onHand: 10);
        var reservation = Reserve(item, quantity: 4);
        var before = InventoryAvailability.Calculate(item, [reservation], StartedAtUtc.AddMinutes(1));

        var released = item.ReleaseReservation(
            reservation,
            "ลูกค้ายกเลิก",
            "release-1",
            expectedVersion: 2,
            StartedAtUtc.AddMinutes(1),
            "system");
        var after = InventoryAvailability.Calculate(item, [reservation], StartedAtUtc.AddMinutes(1));

        Assert.True(released.Changed);
        Assert.Null(released.Movement);
        Assert.Equal(StockReservationStatus.Released, reservation.Status);
        Assert.Equal(before.AvailableQuantity + 4, after.AvailableQuantity);
        Assert.Equal(10, item.ReservableQuantity);
        Assert.Equal(3, item.Version);
        var snapshot = Snapshot(item, reservation);

        var retry = item.ReleaseReservation(
            reservation,
            "ข้อความ retry ไม่สำคัญ",
            "release-1",
            expectedVersion: 1,
            StartedAtUtc.AddMinutes(2),
            "other");
        Assert.False(retry.Changed);
        Assert.Equal(snapshot, Snapshot(item, reservation));
        AssertRule(InventoryRule.ReservationEvidenceConflict, () => item.ReleaseReservation(
            reservation, "retry", "release-other", 3, StartedAtUtc.AddMinutes(2), "system"));
        Assert.Equal(snapshot, Snapshot(item, reservation));
        AssertRule(InventoryRule.ReservationTransitionInvalid, () => item.ConsumeReservation(
            reservation, Guid.NewGuid(), "จ่ายแล้ว", "payment-1", 3, StartedAtUtc.AddMinutes(2), "system"));
        Assert.Equal(snapshot, Snapshot(item, reservation));
    }

    [Fact]
    public void PrematureExpireFailsButPastDueExpireOnlyReleasesPhysicalHold()
    {
        var item = Item(onHand: 10);
        var reservation = Reserve(item, quantity: 4);
        var snapshot = Snapshot(item, reservation);
        AssertRule(InventoryRule.ReservationExpireTooEarly, () => item.ExpireReservation(
            reservation, "หมดเวลา", "expire-1", 2, ExpiresAtUtc.AddTicks(-1), "system"));
        Assert.Equal(snapshot, Snapshot(item, reservation));

        var availableBefore = InventoryAvailability.Calculate(item, [reservation], ExpiresAtUtc).AvailableQuantity;
        var expired = item.ExpireReservation(
            reservation, "provider ยืนยันหมดเวลา", "expire-1", 2, ExpiresAtUtc, "system");
        var availableAfter = InventoryAvailability.Calculate(item, [reservation], ExpiresAtUtc).AvailableQuantity;

        Assert.True(expired.Changed);
        Assert.Equal(availableBefore, availableAfter);
        Assert.Equal(10, item.ReservableQuantity);
        Assert.Equal(StockReservationStatus.Expired, reservation.Status);
    }

    [Fact]
    public void PastDueReleaseLeavesDisplayedAvailableAndRestoresReservable()
    {
        var item = Item(onHand: 10);
        var reservation = Reserve(item, quantity: 4);
        var before = InventoryAvailability.Calculate(item, [reservation], ExpiresAtUtc);

        item.ReleaseReservation(
            reservation,
            "provider ยืนยันว่าไม่จ่าย",
            "release-expired",
            2,
            ExpiresAtUtc,
            "system");
        var after = InventoryAvailability.Calculate(item, [reservation], ExpiresAtUtc);

        Assert.Equal(before.AvailableQuantity, after.AvailableQuantity);
        Assert.Equal(10, item.ReservableQuantity);
    }

    [Fact]
    public void UnexpiredConsumePreservesBothAvailabilityMeasuresAndReturnsLinkedMovement()
    {
        var item = Item(onHand: 10);
        var reservation = Reserve(item, quantity: 4);
        var at = StartedAtUtc.AddMinutes(1);
        var before = InventoryAvailability.Calculate(item, [reservation], at);
        var reservableBefore = item.ReservableQuantity;
        var movementId = Guid.NewGuid();

        var consumed = item.ConsumeReservation(
            reservation,
            movementId,
            "ชำระเงินสำเร็จ",
            "payment-1",
            expectedVersion: 2,
            at,
            "system");
        var after = InventoryAvailability.Calculate(item, [reservation], at);

        Assert.True(consumed.Changed);
        var movement = Assert.IsType<StockMovement>(consumed.Movement);
        Assert.Equal(movementId, movement.Id);
        Assert.Equal(StockMovementType.ReservationConsumed, movement.Type);
        Assert.Equal(-4, movement.QuantityDelta);
        Assert.Equal(reservation.Id, movement.ReservationId);
        Assert.Equal(before.AvailableQuantity, after.AvailableQuantity);
        Assert.Equal(reservableBefore, item.ReservableQuantity);
        Assert.Equal(6, item.OnHandQuantity);
        Assert.Equal(0, item.HeldQuantity);
    }

    [Fact]
    public void PastDueConsumeReducesDisplayedAvailableButLeavesReservableUnchanged()
    {
        var item = Item(onHand: 10);
        var reservation = Reserve(item, quantity: 4);
        var at = ExpiresAtUtc;
        var before = InventoryAvailability.Calculate(item, [reservation], at);
        var reservableBefore = item.ReservableQuantity;

        item.ConsumeReservation(
            reservation,
            Guid.NewGuid(),
            "ชำระก่อนหมดเวลา webhook มาช้า",
            "payment-late",
            2,
            at,
            "system");
        var after = InventoryAvailability.Calculate(item, [reservation], at);

        Assert.Equal(before.AvailableQuantity - 4, after.AvailableQuantity);
        Assert.Equal(reservableBefore, item.ReservableQuantity);
    }

    [Fact]
    public void HeldStockCannotBeAdjustedAwayAndTerminalVersionExhaustionIsAtomic()
    {
        var item = Item(onHand: 5);
        var reservation = Reserve(item, quantity: 4);
        var snapshot = Snapshot(item, reservation);
        AssertRule(InventoryRule.InsufficientOnHand, () => item.AdjustStock(
            Guid.NewGuid(), -2, "ตรวจนับ", "count", 2, StartedAtUtc, "admin"));
        Assert.Equal(snapshot, Snapshot(item, reservation));

        typeof(InventoryItem)
            .GetProperty(nameof(InventoryItem.Version))!
            .SetValue(item, long.MaxValue);
        var exhausted = Snapshot(item, reservation);
        AssertRule(InventoryRule.ConcurrencyVersionExhausted, () => item.ReleaseReservation(
            reservation,
            "หมดเวลา",
            "release-max",
            long.MaxValue,
            StartedAtUtc.AddMinutes(1),
            "system"));
        Assert.Equal(exhausted, Snapshot(item, reservation));
    }

    [Fact]
    public void ConsumeRetryIsNoOpOnlyForMatchingReferenceAndMovementEvidence()
    {
        var item = Item(onHand: 5);
        var reservation = Reserve(item, quantity: 2);
        var movementId = Guid.NewGuid();
        item.ConsumeReservation(
            reservation,
            movementId,
            "ชำระแล้ว",
            "payment-1",
            2,
            StartedAtUtc.AddMinutes(1),
            "system");
        var snapshot = Snapshot(item, reservation);

        var retry = item.ConsumeReservation(
            reservation,
            movementId,
            "retry",
            "payment-1",
            expectedVersion: 1,
            StartedAtUtc,
            "other");
        Assert.False(retry.Changed);
        Assert.Equal(snapshot, Snapshot(item, reservation));
        AssertRule(InventoryRule.ReservationEvidenceConflict, () => item.ConsumeReservation(
            reservation,
            Guid.NewGuid(),
            "retry",
            "payment-1",
            3,
            StartedAtUtc.AddMinutes(2),
            "system"));
        Assert.Equal(snapshot, Snapshot(item, reservation));
    }

    [Fact]
    public void RealTerminalMutationRejectsStaleVersionAndWrongOwnershipWithoutMutation()
    {
        var item = Item(onHand: 5);
        var reservation = Reserve(item, quantity: 2);
        var snapshot = Snapshot(item, reservation);
        AssertRule(InventoryRule.ConcurrencyVersionMismatch, () => item.ReleaseReservation(
            reservation, "ยกเลิก", "release", 1, StartedAtUtc.AddMinutes(1), "system"));
        Assert.Equal(snapshot, Snapshot(item, reservation));

        var wrongItem = Item(Guid.NewGuid(), ProductId, onHand: 2);
        var wrongItemReservation = Reserve(wrongItem, reservationId: Guid.NewGuid(), quantity: 1);
        var wrongItemSnapshot = Snapshot(wrongItem, wrongItemReservation);
        AssertRule(InventoryRule.ReservationInventoryMismatch, () => item.ReleaseReservation(
            wrongItemReservation, "ยกเลิก", "wrong-item", 2, StartedAtUtc.AddMinutes(1), "system"));
        Assert.Equal(snapshot, Snapshot(item, reservation));
        Assert.Equal(wrongItemSnapshot, Snapshot(wrongItem, wrongItemReservation));

        var wrongProductItem = Item(InventoryId, Guid.NewGuid(), onHand: 2);
        var wrongProductReservation = Reserve(
            wrongProductItem,
            reservationId: Guid.NewGuid(),
            quantity: 1);
        var wrongProductSnapshot = Snapshot(wrongProductItem, wrongProductReservation);
        AssertRule(InventoryRule.ReservationProductMismatch, () => item.ReleaseReservation(
            wrongProductReservation,
            "ยกเลิก",
            "wrong-product",
            2,
            StartedAtUtc.AddMinutes(1),
            "system"));
        Assert.Equal(snapshot, Snapshot(item, reservation));
        Assert.Equal(wrongProductSnapshot, Snapshot(wrongProductItem, wrongProductReservation));
    }

    [Fact]
    public void InvalidRequestShapePrecedesSameTerminalIdempotentNoOp()
    {
        var item = Item(onHand: 5);
        var reservation = Reserve(item, quantity: 2);
        item.ReleaseReservation(
            reservation, "ยกเลิก", "release-1", 2, StartedAtUtc.AddMinutes(1), "system");
        var snapshot = Snapshot(item, reservation);

        AssertRule(InventoryRule.ReferenceRequired, () => item.ReleaseReservation(
            reservation, "retry", " ", 1, StartedAtUtc, "other"));
        Assert.Equal(snapshot, Snapshot(item, reservation));
        AssertRule(InventoryRule.AuditInstantMustBeUtc, () => item.ReleaseReservation(
            reservation,
            "retry",
            "release-1",
            1,
            StartedAtUtc.ToOffset(TimeSpan.FromHours(7)),
            "other"));
        Assert.Equal(snapshot, Snapshot(item, reservation));
    }

    private static InventoryItem Item(
        Guid? inventoryId = null,
        Guid? productId = null,
        int onHand = 10) =>
        InventoryItem.Create(
            inventoryId ?? InventoryId,
            productId ?? ProductId,
            Guid.NewGuid(),
            onHand,
            "สินค้าเริ่มต้น",
            "product-create",
            StartedAtUtc,
            "admin").Item;

    private static StockReservation Reserve(
        InventoryItem item,
        Guid? reservationId = null,
        Guid? checkoutAttemptId = null,
        int quantity = 2,
        DateTimeOffset? reservedAtUtc = null,
        DateTimeOffset? expiresAtUtc = null,
        long expectedVersion = 1) =>
        item.Reserve(
            reservationId ?? ReservationId,
            checkoutAttemptId ?? CheckoutAttemptId,
            quantity,
            reservedAtUtc ?? StartedAtUtc,
            expiresAtUtc ?? ExpiresAtUtc,
            "รอชำระเงิน",
            "checkout-reserve",
            expectedVersion,
            "system");

    private static void AssertRule(InventoryRule rule, Action action)
    {
        var exception = Assert.Throws<InventoryRuleException>(action);
        Assert.Equal(rule, exception.Rule);
    }

    private static void AssertUnchanged(InventoryItem item, InventoryRule rule, Action action)
    {
        var before = new { item.HeldQuantity, item.Version, item.UpdatedAtUtc, item.UpdatedBy };
        AssertRule(rule, action);
        Assert.Equal(before, new { item.HeldQuantity, item.Version, item.UpdatedAtUtc, item.UpdatedBy });
    }

    private static object Snapshot(InventoryItem item, StockReservation reservation) => new
    {
        item.OnHandQuantity,
        item.HeldQuantity,
        item.Version,
        item.UpdatedAtUtc,
        item.UpdatedBy,
        reservation.Status,
        reservation.TerminalAtUtc,
        reservation.TerminalActor,
        reservation.TerminalReason,
        reservation.TerminalReference,
        reservation.ConsumedMovementId,
    };
}
