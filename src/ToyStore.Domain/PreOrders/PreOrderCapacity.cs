using ToyStore.Domain.Products;

namespace ToyStore.Domain.PreOrders;

public sealed class PreOrderCapacity
{
    private PreOrderCapacity()
    {
        CreatedBy = null!;
        UpdatedBy = null!;
        Version = 1;
    }

    private PreOrderCapacity(
        Guid id,
        Guid productId,
        int totalCapacity,
        DateTimeOffset closeAtUtc,
        DateTimeOffset createdAtUtc,
        string actor)
    {
        Id = id;
        ProductId = productId;
        TotalCapacity = totalCapacity;
        CloseAtUtc = closeAtUtc;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        CreatedBy = actor;
        UpdatedBy = actor;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public int TotalCapacity { get; private set; }
    public int HeldQuantity { get; private set; }
    public int CommittedQuantity { get; private set; }
    public int RetiredQuantity { get; private set; }
    public int RemainingQuantity => TotalCapacity - HeldQuantity - CommittedQuantity - RetiredQuantity;
    public DateTimeOffset CloseAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public string UpdatedBy { get; private set; }
    public long Version { get; private set; }

    public static PreOrderCapacityCreation Create(
        Guid id,
        Guid productId,
        Guid initialMovementId,
        PreOrderOffer offer,
        string reason,
        string reference,
        DateTimeOffset createdAtUtc,
        string actor)
    {
        if (id == Guid.Empty)
        {
            Fail(PreOrderCapacityRule.CapacityIdentityRequired);
        }

        if (productId == Guid.Empty)
        {
            Fail(PreOrderCapacityRule.ProductIdentityRequired);
        }

        ArgumentNullException.ThrowIfNull(offer);
        PreOrderCapacityEvidence.EnsureUtc(createdAtUtc);
        PreOrderCapacityEvidence.EnsureUtc(offer.CloseAtUtc);
        if (createdAtUtc >= offer.CloseAtUtc)
        {
            Fail(PreOrderCapacityRule.PreOrderClosed);
        }
        var preparedActor = PreOrderCapacityEvidence.PrepareActor(actor);
        var capacity = new PreOrderCapacity(
            id,
            productId,
            offer.TotalCapacity,
            offer.CloseAtUtc,
            createdAtUtc,
            preparedActor);
        var movement = capacity.CreateMovement(
            initialMovementId,
            PreOrderCapacityMovementType.InitialCapacity,
            offer.TotalCapacity,
            offer.TotalCapacity,
            0,
            0,
            0,
            1,
            reason,
            reference,
            createdAtUtc,
            preparedActor);
        return PreOrderCapacityCreation.Create(capacity, movement);
    }

    public PreOrderCapacityReservationCreation Reserve(
        Guid reservationId,
        Guid checkoutAttemptId,
        string customerId,
        int quantity,
        DateTimeOffset reservedAtUtc,
        DateTimeOffset expiresAtUtc,
        Guid movementId,
        string reason,
        string reference,
        long expectedVersion,
        string actor)
    {
        if (reservationId == Guid.Empty)
        {
            Fail(PreOrderCapacityRule.ReservationIdentityRequired);
        }

        if (checkoutAttemptId == Guid.Empty)
        {
            Fail(PreOrderCapacityRule.CheckoutAttemptIdentityRequired);
        }

        EnsurePositive(quantity);
        PreOrderCapacityEvidence.EnsureUtc(reservedAtUtc);
        PreOrderCapacityEvidence.EnsureUtc(expiresAtUtc);
        if (expiresAtUtc <= reservedAtUtc)
        {
            Fail(PreOrderCapacityRule.ReservationExpiryInvalid);
        }

        EnsureExpectedVersion(expectedVersion);
        ValidateAudit(reservedAtUtc);
        if (reservedAtUtc >= CloseAtUtc)
        {
            Fail(PreOrderCapacityRule.PreOrderClosed);
        }

        if (quantity > RemainingQuantity)
        {
            Fail(PreOrderCapacityRule.InsufficientRemainingCapacity);
        }

        var customer = PreOrderCapacityEvidence.PrepareCustomerId(customerId);
        var prepared = PrepareEvidence(reason, reference, actor);
        var nextHeld = CheckedAdd(HeldQuantity, quantity);
        var nextVersion = NextVersion();
        var movement = CreateMovement(
            movementId,
            PreOrderCapacityMovementType.Reserved,
            quantity,
            -quantity,
            nextHeld,
            CommittedQuantity,
            RetiredQuantity,
            nextVersion,
            prepared.Reason,
            prepared.Reference,
            reservedAtUtc,
            prepared.Actor,
            reservationId);
        var reservation = new PreOrderCapacityReservation(
            reservationId,
            Id,
            ProductId,
            checkoutAttemptId,
            customer,
            quantity,
            reservedAtUtc,
            expiresAtUtc,
            movementId,
            prepared.Reason,
            prepared.Reference,
            prepared.Actor);

        HeldQuantity = nextHeld;
        ApplyAudit(reservedAtUtc, prepared.Actor, nextVersion);
        EnsureAccountingInvariant();
        return PreOrderCapacityReservationCreation.Create(reservation, movement);
    }

    public PreOrderCapacityTransitionResult ReleaseReservation(
        PreOrderCapacityReservation reservation,
        Guid movementId,
        string reason,
        string reference,
        long expectedVersion,
        DateTimeOffset changedAtUtc,
        string actor) =>
        ReleaseOrExpire(
            reservation,
            movementId,
            PreOrderCapacityReservationStatus.Released,
            PreOrderCapacityMovementType.Released,
            reason,
            reference,
            expectedVersion,
            changedAtUtc,
            actor);

    public PreOrderCapacityTransitionResult ExpireReservation(
        PreOrderCapacityReservation reservation,
        Guid movementId,
        string reason,
        string reference,
        long expectedVersion,
        DateTimeOffset changedAtUtc,
        string actor) =>
        ReleaseOrExpire(
            reservation,
            movementId,
            PreOrderCapacityReservationStatus.Expired,
            PreOrderCapacityMovementType.Expired,
            reason,
            reference,
            expectedVersion,
            changedAtUtc,
            actor);

    public PreOrderCapacityTransitionResult ConsumeReservation(
        PreOrderCapacityReservation reservation,
        Guid movementId,
        string reason,
        string reference,
        long expectedVersion,
        DateTimeOffset changedAtUtc,
        string actor)
    {
        ValidateOwnership(reservation);
        var prepared = PrepareTransitionEvidence(movementId, reason, reference, changedAtUtc, actor);
        if (reservation.Status != PreOrderCapacityReservationStatus.Active)
        {
            return ExactRetry(
                reservation,
                PreOrderCapacityReservationStatus.Consumed,
                movementId,
                prepared.Reference);
        }

        ValidateMutation(reservation, expectedVersion, changedAtUtc);
        if (HeldQuantity < reservation.Quantity)
        {
            Fail(PreOrderCapacityRule.HeldQuantityInvariant);
        }

        var nextHeld = HeldQuantity - reservation.Quantity;
        var nextCommitted = CheckedAdd(CommittedQuantity, reservation.Quantity);
        var nextVersion = NextVersion();
        var movement = CreateMovement(
            movementId,
            PreOrderCapacityMovementType.ReservationConsumed,
            reservation.Quantity,
            0,
            nextHeld,
            nextCommitted,
            RetiredQuantity,
            nextVersion,
            prepared.Reason,
            prepared.Reference,
            changedAtUtc,
            prepared.Actor,
            reservation.Id);

        HeldQuantity = nextHeld;
        CommittedQuantity = nextCommitted;
        reservation.ApplyTransition(
            PreOrderCapacityReservationStatus.Consumed,
            changedAtUtc,
            prepared.Actor,
            prepared.Reason,
            prepared.Reference,
            movementId,
            movementId);
        ApplyAudit(changedAtUtc, prepared.Actor, nextVersion);
        EnsureAccountingInvariant();
        return PreOrderCapacityTransitionResult.ChangedWith(movement);
    }

    public PreOrderCapacityTransitionResult CancelReservation(
        PreOrderCapacityReservation reservation,
        Guid movementId,
        PreOrderCancellationKind cancellationKind,
        string reason,
        string reference,
        long expectedVersion,
        DateTimeOffset changedAtUtc,
        string actor)
    {
        ValidateOwnership(reservation);
        if (!Enum.IsDefined(cancellationKind))
        {
            Fail(PreOrderCapacityRule.CancellationKindInvalid);
        }

        var prepared = PrepareTransitionEvidence(movementId, reason, reference, changedAtUtc, actor);
        if (reservation.Status == PreOrderCapacityReservationStatus.Cancelled)
        {
            if (reservation.TransitionMovementId != movementId
                || reservation.CancellationKind != cancellationKind
                || !string.Equals(reservation.TransitionReference, prepared.Reference, StringComparison.Ordinal))
            {
                Fail(PreOrderCapacityRule.ReservationEvidenceConflict);
            }

            return PreOrderCapacityTransitionResult.Unchanged();
        }

        if (reservation.Status != PreOrderCapacityReservationStatus.Consumed)
        {
            Fail(PreOrderCapacityRule.ReservationTransitionInvalid);
        }

        ValidateMutation(reservation, expectedVersion, changedAtUtc);
        if (CommittedQuantity < reservation.Quantity)
        {
            Fail(PreOrderCapacityRule.CommittedQuantityInvariant);
        }

        var reopens = changedAtUtc < CloseAtUtc;
        var nextCommitted = CommittedQuantity - reservation.Quantity;
        var nextRetired = reopens ? RetiredQuantity : CheckedAdd(RetiredQuantity, reservation.Quantity);
        var nextVersion = NextVersion();
        var movement = CreateMovement(
            movementId,
            reopens
                ? PreOrderCapacityMovementType.CancellationReopened
                : PreOrderCapacityMovementType.CancellationRetired,
            reservation.Quantity,
            reopens ? reservation.Quantity : 0,
            HeldQuantity,
            nextCommitted,
            nextRetired,
            nextVersion,
            prepared.Reason,
            prepared.Reference,
            changedAtUtc,
            prepared.Actor,
            reservation.Id);
        var disposition = cancellationKind == PreOrderCancellationKind.AdminOrSupplier
            ? PreOrderDepositDisposition.RefundRequired
            : PreOrderDepositDisposition.Forfeited;

        CommittedQuantity = nextCommitted;
        RetiredQuantity = nextRetired;
        reservation.ApplyTransition(
            PreOrderCapacityReservationStatus.Cancelled,
            changedAtUtc,
            prepared.Actor,
            prepared.Reason,
            prepared.Reference,
            movementId,
            reservation.ConsumedMovementId,
            cancellationKind,
            disposition);
        ApplyAudit(changedAtUtc, prepared.Actor, nextVersion);
        EnsureAccountingInvariant();
        return PreOrderCapacityTransitionResult.ChangedWith(movement);
    }

    private PreOrderCapacityTransitionResult ReleaseOrExpire(
        PreOrderCapacityReservation reservation,
        Guid movementId,
        PreOrderCapacityReservationStatus status,
        PreOrderCapacityMovementType type,
        string reason,
        string reference,
        long expectedVersion,
        DateTimeOffset changedAtUtc,
        string actor)
    {
        ValidateOwnership(reservation);
        var prepared = PrepareTransitionEvidence(movementId, reason, reference, changedAtUtc, actor);
        if (reservation.Status != PreOrderCapacityReservationStatus.Active)
        {
            return ExactRetry(reservation, status, movementId, prepared.Reference);
        }

        if (status == PreOrderCapacityReservationStatus.Expired
            && changedAtUtc < reservation.ExpiresAtUtc)
        {
            Fail(PreOrderCapacityRule.ReservationExpireTooEarly);
        }

        ValidateMutation(reservation, expectedVersion, changedAtUtc);
        if (HeldQuantity < reservation.Quantity)
        {
            Fail(PreOrderCapacityRule.HeldQuantityInvariant);
        }

        var nextHeld = HeldQuantity - reservation.Quantity;
        var nextVersion = NextVersion();
        var movement = CreateMovement(
            movementId,
            type,
            reservation.Quantity,
            reservation.Quantity,
            nextHeld,
            CommittedQuantity,
            RetiredQuantity,
            nextVersion,
            prepared.Reason,
            prepared.Reference,
            changedAtUtc,
            prepared.Actor,
            reservation.Id);

        HeldQuantity = nextHeld;
        reservation.ApplyTransition(
            status,
            changedAtUtc,
            prepared.Actor,
            prepared.Reason,
            prepared.Reference,
            movementId);
        ApplyAudit(changedAtUtc, prepared.Actor, nextVersion);
        EnsureAccountingInvariant();
        return PreOrderCapacityTransitionResult.ChangedWith(movement);
    }

    private static PreOrderCapacityTransitionResult ExactRetry(
        PreOrderCapacityReservation reservation,
        PreOrderCapacityReservationStatus status,
        Guid movementId,
        string reference)
    {
        if (reservation.Status != status)
        {
            Fail(PreOrderCapacityRule.ReservationTransitionInvalid);
        }

        if (reservation.TransitionMovementId != movementId
            || !string.Equals(reservation.TransitionReference, reference, StringComparison.Ordinal))
        {
            Fail(PreOrderCapacityRule.ReservationEvidenceConflict);
        }

        return PreOrderCapacityTransitionResult.Unchanged();
    }

    private void ValidateOwnership(PreOrderCapacityReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (reservation.CapacityId != Id)
        {
            Fail(PreOrderCapacityRule.ReservationCapacityMismatch);
        }

        if (reservation.ProductId != ProductId)
        {
            Fail(PreOrderCapacityRule.ReservationProductMismatch);
        }
    }

    private static Evidence PrepareTransitionEvidence(
        Guid movementId,
        string reason,
        string reference,
        DateTimeOffset changedAtUtc,
        string actor)
    {
        if (movementId == Guid.Empty)
        {
            Fail(PreOrderCapacityRule.MovementIdentityRequired);
        }

        PreOrderCapacityEvidence.EnsureUtc(changedAtUtc);
        return PrepareEvidence(reason, reference, actor);
    }

    private static Evidence PrepareEvidence(string reason, string reference, string actor) =>
        new(
            PreOrderCapacityEvidence.PrepareReason(reason),
            PreOrderCapacityEvidence.PrepareReference(reference),
            PreOrderCapacityEvidence.PrepareActor(actor));

    private void ValidateMutation(
        PreOrderCapacityReservation reservation,
        long expectedVersion,
        DateTimeOffset changedAtUtc)
    {
        EnsureExpectedVersion(expectedVersion);
        ValidateAudit(changedAtUtc);
        if (changedAtUtc < reservation.ReservedAtUtc)
        {
            Fail(PreOrderCapacityRule.AuditTimeWentBackwards);
        }
    }

    private PreOrderCapacityMovement CreateMovement(
        Guid id,
        PreOrderCapacityMovementType type,
        int quantity,
        int availableDelta,
        int held,
        int committed,
        int retired,
        long version,
        string reason,
        string reference,
        DateTimeOffset occurredAtUtc,
        string actor,
        Guid? reservationId = null)
    {
        var remaining = TotalCapacity - held - committed - retired;
        if (remaining < 0 || held < 0 || committed < 0 || retired < 0)
        {
            Fail(PreOrderCapacityRule.CapacityAccountingInvariant);
        }

        return PreOrderCapacityMovement.Create(
            id,
            Id,
            ProductId,
            type,
            quantity,
            availableDelta,
            remaining,
            held,
            committed,
            retired,
            version,
            reason,
            reference,
            occurredAtUtc,
            actor,
            reservationId);
    }

    private static int CheckedAdd(int value, int quantity)
    {
        try
        {
            return checked(value + quantity);
        }
        catch (OverflowException)
        {
            throw new PreOrderCapacityRuleException(PreOrderCapacityRule.QuantityOverflow);
        }
    }

    private static void EnsurePositive(int quantity)
    {
        if (quantity <= 0)
        {
            Fail(PreOrderCapacityRule.QuantityMustBePositive);
        }
    }

    private void EnsureExpectedVersion(long expectedVersion)
    {
        if (Version != expectedVersion)
        {
            Fail(PreOrderCapacityRule.ConcurrencyVersionMismatch);
        }
    }

    private long NextVersion()
    {
        if (Version == long.MaxValue)
        {
            Fail(PreOrderCapacityRule.ConcurrencyVersionExhausted);
        }

        return Version + 1;
    }

    private void ValidateAudit(DateTimeOffset changedAtUtc)
    {
        PreOrderCapacityEvidence.EnsureUtc(changedAtUtc);
        if (changedAtUtc < UpdatedAtUtc)
        {
            Fail(PreOrderCapacityRule.AuditTimeWentBackwards);
        }
    }

    private void ApplyAudit(DateTimeOffset atUtc, string actor, long version)
    {
        UpdatedAtUtc = atUtc;
        UpdatedBy = actor;
        Version = version;
    }

    private void EnsureAccountingInvariant()
    {
        if (RemainingQuantity < 0
            || HeldQuantity < 0
            || CommittedQuantity < 0
            || RetiredQuantity < 0
            || RemainingQuantity + HeldQuantity + CommittedQuantity + RetiredQuantity != TotalCapacity)
        {
            Fail(PreOrderCapacityRule.CapacityAccountingInvariant);
        }
    }

    private static void Fail(PreOrderCapacityRule rule) =>
        throw new PreOrderCapacityRuleException(rule);

    private sealed record Evidence(string Reason, string Reference, string Actor);
}
