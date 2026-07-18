using System.Reflection;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.Products;

namespace ToyStore.UnitTests.Domain.Inventory;

public sealed class InventoryItemStockTests
{
    private static readonly Guid InventoryId = Guid.Parse("71000000-0000-0000-0000-000000000001");
    private static readonly Guid ProductId = Guid.Parse("71000000-0000-0000-0000-000000000002");
    private static readonly Guid MovementId = Guid.Parse("71000000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 7, 17, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PublicShapeKeepsInventorySeparateAndEvidenceImmutable()
    {
        Assert.DoesNotContain(
            typeof(Product).GetProperties(),
            property => property.Name is "Stock" or "Inventory" or "InventoryItem"
                or "OnHandQuantity" or "HeldQuantity" or "AvailableQuantity");

        foreach (var type in new[]
                 {
                     typeof(InventoryItem),
                     typeof(StockMovement),
                     typeof(InventoryCreation),
                 })
        {
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
            Assert.All(type.GetProperties(), property =>
                Assert.False(property.SetMethod?.IsPublic ?? false));
        }

        Assert.Equal(
            [
                StockMovementType.InitialStock,
                StockMovementType.Received,
                StockMovementType.Adjusted,
                StockMovementType.ReservationConsumed,
            ],
            Enum.GetValues<StockMovementType>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    public void CreateReturnsVersionOneItemAndInitialEvidenceIncludingZero(int initialStock)
    {
        var creation = Create(initialStock: initialStock);
        var item = creation.Item;
        var movement = creation.InitialMovement;

        Assert.Equal(InventoryId, item.Id);
        Assert.Equal(ProductId, item.ProductId);
        Assert.Equal(initialStock, item.OnHandQuantity);
        Assert.Equal(0, item.HeldQuantity);
        Assert.Equal(initialStock, item.ReservableQuantity);
        Assert.Equal(1, item.Version);
        Assert.Equal(CreatedAtUtc, item.CreatedAtUtc);
        Assert.Equal("admin-1", item.CreatedBy);
        Assert.Equal(CreatedAtUtc, item.UpdatedAtUtc);
        Assert.Equal("admin-1", item.UpdatedBy);
        AssertMovement(
            movement,
            MovementId,
            StockMovementType.InitialStock,
            initialStock,
            initialStock,
            resultingVersion: 1,
            reservationId: null);
    }

    [Fact]
    public void CreateRejectsInvalidIdentityQuantityAuditAndBoundedEvidence()
    {
        AssertRule(InventoryRule.InventoryIdentityRequired, () => Create(id: Guid.Empty));
        AssertRule(InventoryRule.ProductIdentityRequired, () => Create(productId: Guid.Empty));
        AssertRule(InventoryRule.MovementIdentityRequired, () => Create(movementId: Guid.Empty));
        AssertRule(InventoryRule.QuantityCannotBeNegative, () => Create(initialStock: -1));
        AssertRule(
            InventoryRule.AuditInstantMustBeUtc,
            () => Create(createdAtUtc: CreatedAtUtc.ToOffset(TimeSpan.FromHours(7))));
        AssertRule(InventoryRule.ActorRequired, () => Create(actor: " "));
        AssertRule(InventoryRule.ReasonRequired, () => Create(reason: " "));
        AssertRule(InventoryRule.ReferenceRequired, () => Create(reference: " "));
        AssertRule(
            InventoryRule.ActorTooLong,
            () => Create(actor: new string('a', InventoryLimits.ActorLength + 1)));
        AssertRule(
            InventoryRule.ReasonTooLong,
            () => Create(reason: new string('r', InventoryLimits.ReasonLength + 1)));
        AssertRule(
            InventoryRule.ReferenceTooLong,
            () => Create(reference: new string('x', InventoryLimits.ReferenceLength + 1)));
    }

    [Fact]
    public void ReceiveAndAdjustReturnOneMovementAndAdvanceAuditAndVersionOnce()
    {
        var item = Create(initialStock: 5).Item;
        var received = item.ReceiveStock(
            Guid.NewGuid(),
            quantity: 4,
            reason: "รับสินค้าเข้า",
            reference: "receive-1",
            expectedVersion: 1,
            changedAtUtc: CreatedAtUtc,
            actor: "admin-2");

        Assert.Equal(9, item.OnHandQuantity);
        Assert.Equal(2, item.Version);
        Assert.Equal(CreatedAtUtc, item.UpdatedAtUtc);
        Assert.Equal("admin-2", item.UpdatedBy);
        Assert.Equal(StockMovementType.Received, received.Type);
        Assert.Equal(4, received.QuantityDelta);
        Assert.Equal(9, received.ResultingOnHandQuantity);
        Assert.Equal(2, received.ResultingInventoryVersion);

        var adjusted = item.AdjustStock(
            Guid.NewGuid(),
            quantityDelta: -3,
            reason: "ตรวจนับสินค้า",
            reference: "count-1",
            expectedVersion: 2,
            changedAtUtc: CreatedAtUtc.AddMinutes(1),
            actor: "admin-3");

        Assert.Equal(6, item.OnHandQuantity);
        Assert.Equal(3, item.Version);
        Assert.Equal(StockMovementType.Adjusted, adjusted.Type);
        Assert.Equal(-3, adjusted.QuantityDelta);
        Assert.Equal(6, adjusted.ResultingOnHandQuantity);
        Assert.Equal(3, adjusted.ResultingInventoryVersion);
    }

    [Fact]
    public void InvalidReceiveAndAdjustLeaveTheCompleteSnapshotUnchanged()
    {
        var item = Create(initialStock: 5).Item;

        AssertUnchanged(item, InventoryRule.QuantityMustBePositive, () => item.ReceiveStock(
            Guid.NewGuid(), 0, "รับเข้า", "receive", 1, CreatedAtUtc, "admin"));
        AssertUnchanged(item, InventoryRule.AdjustmentCannotBeZero, () => item.AdjustStock(
            Guid.NewGuid(), 0, "ปรับ", "adjust", 1, CreatedAtUtc, "admin"));
        AssertUnchanged(item, InventoryRule.InsufficientOnHand, () => item.AdjustStock(
            Guid.NewGuid(), -6, "ปรับ", "adjust", 1, CreatedAtUtc, "admin"));
        AssertUnchanged(item, InventoryRule.ConcurrencyVersionMismatch, () => item.ReceiveStock(
            Guid.NewGuid(), 1, "รับ", "receive", 2, CreatedAtUtc, "admin"));
        AssertUnchanged(item, InventoryRule.AuditTimeWentBackwards, () => item.ReceiveStock(
            Guid.NewGuid(), 1, "รับ", "receive", 1, CreatedAtUtc.AddTicks(-1), "admin"));

        var maximum = Create(initialStock: int.MaxValue).Item;
        AssertUnchanged(maximum, InventoryRule.QuantityOverflow, () => maximum.ReceiveStock(
            Guid.NewGuid(), 1, "รับ", "receive", 1, CreatedAtUtc, "admin"));
    }

    [Fact]
    public void VersionExhaustionAndCrossMutationBackwardsAuditAreAtomic()
    {
        var item = Create(initialStock: 5).Item;
        item.ReceiveStock(
            Guid.NewGuid(), 1, "รับ", "receive", 1, CreatedAtUtc.AddMinutes(1), "admin");
        AssertUnchanged(item, InventoryRule.AuditTimeWentBackwards, () => item.AdjustStock(
            Guid.NewGuid(), -1, "ตรวจนับ", "count", 2, CreatedAtUtc, "admin"));

        typeof(InventoryItem)
            .GetProperty(nameof(InventoryItem.Version))!
            .SetValue(item, long.MaxValue);
        AssertUnchanged(item, InventoryRule.ConcurrencyVersionExhausted, () => item.ReceiveStock(
            Guid.NewGuid(), 1, "รับ", "receive-2", long.MaxValue, CreatedAtUtc.AddMinutes(2), "admin"));
    }

    private static InventoryCreation Create(
        Guid? id = null,
        Guid? productId = null,
        Guid? movementId = null,
        int initialStock = 5,
        string reason = "สินค้าเริ่มต้น",
        string reference = "product-create",
        DateTimeOffset? createdAtUtc = null,
        string actor = "admin-1") =>
        InventoryItem.Create(
            id ?? InventoryId,
            productId ?? ProductId,
            movementId ?? MovementId,
            initialStock,
            reason,
            reference,
            createdAtUtc ?? CreatedAtUtc,
            actor);

    private static void AssertMovement(
        StockMovement movement,
        Guid id,
        StockMovementType type,
        int delta,
        int resultingOnHand,
        long resultingVersion,
        Guid? reservationId)
    {
        Assert.Equal(id, movement.Id);
        Assert.Equal(InventoryId, movement.InventoryItemId);
        Assert.Equal(ProductId, movement.ProductId);
        Assert.Equal(type, movement.Type);
        Assert.Equal(delta, movement.QuantityDelta);
        Assert.Equal(resultingOnHand, movement.ResultingOnHandQuantity);
        Assert.Equal(resultingVersion, movement.ResultingInventoryVersion);
        Assert.Equal("สินค้าเริ่มต้น", movement.Reason);
        Assert.Equal("product-create", movement.Reference);
        Assert.Equal("admin-1", movement.Actor);
        Assert.Equal(CreatedAtUtc, movement.OccurredAtUtc);
        Assert.Equal(reservationId, movement.ReservationId);
    }

    private static void AssertRule(InventoryRule rule, Action action)
    {
        var exception = Assert.Throws<InventoryRuleException>(action);
        Assert.Equal(rule, exception.Rule);
    }

    private static void AssertUnchanged(InventoryItem item, InventoryRule rule, Action action)
    {
        var before = Snapshot(item);
        AssertRule(rule, action);
        Assert.Equal(before, Snapshot(item));
    }

    private static object Snapshot(InventoryItem item) => new
    {
        item.OnHandQuantity,
        item.HeldQuantity,
        item.Version,
        item.UpdatedAtUtc,
        item.UpdatedBy,
    };
}
