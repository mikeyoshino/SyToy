namespace ToyStore.Domain.Inventory;

public sealed class InventoryItem
{
    private InventoryItem()
    {
        CreatedBy = null!;
        UpdatedBy = null!;
        Version = 1;
    }

    private InventoryItem(
        Guid id,
        Guid productId,
        int onHandQuantity,
        DateTimeOffset createdAtUtc,
        string actor)
    {
        Id = id;
        ProductId = productId;
        OnHandQuantity = onHandQuantity;
        HeldQuantity = 0;
        CreatedAtUtc = createdAtUtc;
        CreatedBy = actor;
        UpdatedAtUtc = createdAtUtc;
        UpdatedBy = actor;
        Version = 1;
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public int OnHandQuantity { get; private set; }

    public int HeldQuantity { get; private set; }

    public int ReservableQuantity => OnHandQuantity - HeldQuantity;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public string CreatedBy { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public string UpdatedBy { get; private set; }

    public long Version { get; private set; }

    public static InventoryCreation Create(
        Guid id,
        Guid productId,
        Guid initialMovementId,
        int initialStock,
        string reason,
        string reference,
        DateTimeOffset createdAtUtc,
        string actor)
    {
        if (id == Guid.Empty)
        {
            throw new InventoryRuleException(InventoryRule.InventoryIdentityRequired);
        }

        if (productId == Guid.Empty)
        {
            throw new InventoryRuleException(InventoryRule.ProductIdentityRequired);
        }

        if (initialStock < 0)
        {
            throw new InventoryRuleException(InventoryRule.QuantityCannotBeNegative);
        }

        InventoryEvidence.EnsureUtc(createdAtUtc);
        var preparedActor = InventoryEvidence.PrepareActor(actor);
        var item = new InventoryItem(id, productId, initialStock, createdAtUtc, preparedActor);
        var movement = StockMovement.Create(
            initialMovementId,
            id,
            productId,
            StockMovementType.InitialStock,
            initialStock,
            initialStock,
            resultingInventoryVersion: 1,
            reason,
            reference,
            createdAtUtc,
            preparedActor);
        return InventoryCreation.Create(item, movement);
    }

    public StockMovement ReceiveStock(
        Guid movementId,
        int quantity,
        string reason,
        string reference,
        long expectedVersion,
        DateTimeOffset changedAtUtc,
        string actor)
    {
        if (quantity <= 0)
        {
            throw new InventoryRuleException(InventoryRule.QuantityMustBePositive);
        }

        int resultingOnHand;
        try
        {
            resultingOnHand = checked(OnHandQuantity + quantity);
        }
        catch (OverflowException)
        {
            throw new InventoryRuleException(InventoryRule.QuantityOverflow);
        }

        return ApplyStockMutation(
            movementId,
            StockMovementType.Received,
            quantity,
            resultingOnHand,
            reason,
            reference,
            expectedVersion,
            changedAtUtc,
            actor);
    }

    public StockMovement AdjustStock(
        Guid movementId,
        int quantityDelta,
        string reason,
        string reference,
        long expectedVersion,
        DateTimeOffset changedAtUtc,
        string actor)
    {
        if (quantityDelta == 0)
        {
            throw new InventoryRuleException(InventoryRule.AdjustmentCannotBeZero);
        }

        int resultingOnHand;
        try
        {
            resultingOnHand = checked(OnHandQuantity + quantityDelta);
        }
        catch (OverflowException)
        {
            throw new InventoryRuleException(InventoryRule.QuantityOverflow);
        }

        if (resultingOnHand < 0 || resultingOnHand < HeldQuantity)
        {
            throw new InventoryRuleException(InventoryRule.InsufficientOnHand);
        }

        return ApplyStockMutation(
            movementId,
            StockMovementType.Adjusted,
            quantityDelta,
            resultingOnHand,
            reason,
            reference,
            expectedVersion,
            changedAtUtc,
            actor);
    }

    public StockReservation Reserve(
        Guid reservationId,
        Guid checkoutAttemptId,
        int quantity,
        DateTimeOffset reservedAtUtc,
        DateTimeOffset expiresAtUtc,
        string reason,
        string reference,
        long expectedVersion,
        string actor)
    {
        if (reservationId == Guid.Empty)
        {
            throw new InventoryRuleException(InventoryRule.ReservationIdentityRequired);
        }

        if (checkoutAttemptId == Guid.Empty)
        {
            throw new InventoryRuleException(InventoryRule.CheckoutAttemptIdentityRequired);
        }

        if (quantity <= 0)
        {
            throw new InventoryRuleException(InventoryRule.QuantityMustBePositive);
        }

        InventoryEvidence.EnsureUtc(reservedAtUtc);
        InventoryEvidence.EnsureUtc(expiresAtUtc);
        if (expiresAtUtc <= reservedAtUtc)
        {
            throw new InventoryRuleException(InventoryRule.ReservationExpiryInvalid);
        }

        EnsureExpectedVersion(expectedVersion);
        ValidateAudit(reservedAtUtc);
        var preparedReason = InventoryEvidence.PrepareReason(reason);
        var preparedReference = InventoryEvidence.PrepareReference(reference);
        var preparedActor = InventoryEvidence.PrepareActor(actor);
        if (quantity > ReservableQuantity)
        {
            throw new InventoryRuleException(InventoryRule.InsufficientReservableQuantity);
        }

        int nextHeld;
        try
        {
            nextHeld = checked(HeldQuantity + quantity);
        }
        catch (OverflowException)
        {
            throw new InventoryRuleException(InventoryRule.QuantityOverflow);
        }

        var nextVersion = NextVersion();
        var reservation = new StockReservation(
            reservationId,
            Id,
            ProductId,
            checkoutAttemptId,
            quantity,
            reservedAtUtc,
            expiresAtUtc,
            preparedReason,
            preparedReference,
            preparedActor);
        HeldQuantity = nextHeld;
        ApplyAudit(reservedAtUtc, preparedActor, nextVersion);
        return reservation;
    }

    public ReservationTransitionResult ReleaseReservation(
        StockReservation reservation,
        string reason,
        string reference,
        long expectedVersion,
        DateTimeOffset changedAtUtc,
        string actor) =>
        TransitionReservation(
            reservation,
            StockReservationStatus.Released,
            movementId: null,
            reason,
            reference,
            expectedVersion,
            changedAtUtc,
            actor);

    public ReservationTransitionResult ExpireReservation(
        StockReservation reservation,
        string reason,
        string reference,
        long expectedVersion,
        DateTimeOffset changedAtUtc,
        string actor) =>
        TransitionReservation(
            reservation,
            StockReservationStatus.Expired,
            movementId: null,
            reason,
            reference,
            expectedVersion,
            changedAtUtc,
            actor);

    public ReservationTransitionResult ConsumeReservation(
        StockReservation reservation,
        Guid movementId,
        string reason,
        string reference,
        long expectedVersion,
        DateTimeOffset changedAtUtc,
        string actor) =>
        TransitionReservation(
            reservation,
            StockReservationStatus.Consumed,
            movementId,
            reason,
            reference,
            expectedVersion,
            changedAtUtc,
            actor);

    private ReservationTransitionResult TransitionReservation(
        StockReservation reservation,
        StockReservationStatus targetStatus,
        Guid? movementId,
        string reason,
        string reference,
        long expectedVersion,
        DateTimeOffset changedAtUtc,
        string actor)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (reservation.InventoryItemId != Id)
        {
            throw new InventoryRuleException(InventoryRule.ReservationInventoryMismatch);
        }

        if (reservation.ProductId != ProductId)
        {
            throw new InventoryRuleException(InventoryRule.ReservationProductMismatch);
        }

        InventoryEvidence.EnsureUtc(changedAtUtc);
        var preparedReason = InventoryEvidence.PrepareReason(reason);
        var preparedReference = InventoryEvidence.PrepareReference(reference);
        var preparedActor = InventoryEvidence.PrepareActor(actor);
        if (targetStatus == StockReservationStatus.Consumed
            && (!movementId.HasValue || movementId.Value == Guid.Empty))
        {
            throw new InventoryRuleException(InventoryRule.MovementIdentityRequired);
        }

        if (reservation.Status != StockReservationStatus.Active)
        {
            if (reservation.Status != targetStatus)
            {
                throw new InventoryRuleException(InventoryRule.ReservationTransitionInvalid);
            }

            if (!string.Equals(
                    reservation.TerminalReference,
                    preparedReference,
                    StringComparison.Ordinal)
                || (targetStatus == StockReservationStatus.Consumed
                    && reservation.ConsumedMovementId != movementId))
            {
                throw new InventoryRuleException(InventoryRule.ReservationEvidenceConflict);
            }

            return ReservationTransitionResult.Unchanged();
        }

        if (targetStatus == StockReservationStatus.Expired
            && changedAtUtc < reservation.ExpiresAtUtc)
        {
            throw new InventoryRuleException(InventoryRule.ReservationExpireTooEarly);
        }

        EnsureExpectedVersion(expectedVersion);
        ValidateAudit(changedAtUtc);
        if (changedAtUtc < reservation.ReservedAtUtc)
        {
            throw new InventoryRuleException(InventoryRule.AuditTimeWentBackwards);
        }

        if (HeldQuantity < reservation.Quantity)
        {
            throw new InventoryRuleException(InventoryRule.HeldQuantityInvariant);
        }

        var nextHeld = HeldQuantity - reservation.Quantity;
        var nextOnHand = OnHandQuantity;
        if (targetStatus == StockReservationStatus.Consumed)
        {
            nextOnHand -= reservation.Quantity;
            if (nextOnHand < 0 || nextOnHand < nextHeld)
            {
                throw new InventoryRuleException(InventoryRule.InsufficientOnHand);
            }
        }

        var nextVersion = NextVersion();
        StockMovement? movement = null;
        if (targetStatus == StockReservationStatus.Consumed)
        {
            movement = StockMovement.Create(
                movementId!.Value,
                Id,
                ProductId,
                StockMovementType.ReservationConsumed,
                -reservation.Quantity,
                nextOnHand,
                nextVersion,
                preparedReason,
                preparedReference,
                changedAtUtc,
                preparedActor,
                reservation.Id);
        }

        HeldQuantity = nextHeld;
        OnHandQuantity = nextOnHand;
        reservation.ApplyTerminal(
            targetStatus,
            changedAtUtc,
            preparedActor,
            preparedReason,
            preparedReference,
            movementId);
        ApplyAudit(changedAtUtc, preparedActor, nextVersion);
        return movement is null
            ? ReservationTransitionResult.ChangedWithoutMovement()
            : ReservationTransitionResult.ChangedWithMovement(movement);
    }

    private StockMovement ApplyStockMutation(
        Guid movementId,
        StockMovementType type,
        int quantityDelta,
        int resultingOnHand,
        string reason,
        string reference,
        long expectedVersion,
        DateTimeOffset changedAtUtc,
        string actor)
    {
        EnsureExpectedVersion(expectedVersion);
        ValidateAudit(changedAtUtc);
        var nextVersion = NextVersion();
        var preparedActor = InventoryEvidence.PrepareActor(actor);
        var movement = StockMovement.Create(
            movementId,
            Id,
            ProductId,
            type,
            quantityDelta,
            resultingOnHand,
            nextVersion,
            reason,
            reference,
            changedAtUtc,
            preparedActor);

        OnHandQuantity = resultingOnHand;
        ApplyAudit(changedAtUtc, preparedActor, nextVersion);
        return movement;
    }

    private void EnsureExpectedVersion(long expectedVersion)
    {
        if (expectedVersion != Version)
        {
            throw new InventoryRuleException(InventoryRule.ConcurrencyVersionMismatch);
        }
    }

    private long NextVersion()
    {
        if (Version == long.MaxValue)
        {
            throw new InventoryRuleException(InventoryRule.ConcurrencyVersionExhausted);
        }

        return Version + 1;
    }

    private void ValidateAudit(DateTimeOffset changedAtUtc)
    {
        InventoryEvidence.EnsureUtc(changedAtUtc);
        if (changedAtUtc < UpdatedAtUtc)
        {
            throw new InventoryRuleException(InventoryRule.AuditTimeWentBackwards);
        }
    }

    private void ApplyAudit(DateTimeOffset changedAtUtc, string actor, long version)
    {
        UpdatedAtUtc = changedAtUtc;
        UpdatedBy = actor;
        Version = version;
    }
}
