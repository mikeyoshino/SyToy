using ToyStore.Domain.Carts;

namespace ToyStore.UnitTests.Domain.Carts;

public sealed class CartOperationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 17, 5, 0, 0, TimeSpan.Zero);
    private static readonly string Fingerprint = new('a', CartOperation.FingerprintLength);

    [Fact]
    public void CreateCapturesSafeImmutableRetryEvidence()
    {
        var operationId = Guid.NewGuid();
        var cartId = Guid.NewGuid();

        var operation = CartOperation.Create(
            operationId, cartId, CartOperationType.Add, Fingerprint, 2, 4, null, Now);

        Assert.Equal(operationId, operation.Id);
        Assert.Equal(cartId, operation.CartId);
        Assert.Equal(CartOperationType.Add, operation.Type);
        Assert.Equal(Fingerprint, operation.IntentFingerprint);
        Assert.Equal(2, operation.ResultingCartVersion);
        Assert.Equal(4, operation.ResultingTotalQuantity);
        Assert.Null(operation.ResultData);
        Assert.Equal(Now, operation.OccurredAtUtc);
        Assert.DoesNotContain(typeof(CartOperation).GetProperties(), property =>
            property.Name.Contains("Price", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("Stock", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("Payload", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateRejectsMalformedEvidence()
    {
        AssertRule(CartRule.OperationIdentityRequired, () => CartOperation.Create(
            Guid.Empty, Guid.NewGuid(), CartOperationType.Add, Fingerprint, 1, 1, null, Now));
        AssertRule(CartRule.CartIdentityRequired, () => CartOperation.Create(
            Guid.NewGuid(), Guid.Empty, CartOperationType.Add, Fingerprint, 1, 1, null, Now));
        AssertRule(CartRule.OperationTypeInvalid, () => CartOperation.Create(
            Guid.NewGuid(), Guid.NewGuid(), (CartOperationType)99, Fingerprint, 1, 1, null, Now));
        AssertRule(CartRule.OperationFingerprintInvalid, () => CartOperation.Create(
            Guid.NewGuid(), Guid.NewGuid(), CartOperationType.Add, "ABC", 1, 1, null, Now));
        AssertRule(CartRule.OperationResultVersionInvalid, () => CartOperation.Create(
            Guid.NewGuid(), Guid.NewGuid(), CartOperationType.Add, Fingerprint, 0, 1, null, Now));
        AssertRule(CartRule.OperationResultTotalInvalid, () => CartOperation.Create(
            Guid.NewGuid(), Guid.NewGuid(), CartOperationType.Add, Fingerprint, 1, -1, null, Now));
        AssertRule(CartRule.OperationResultDataInvalid, () => CartOperation.Create(
            Guid.NewGuid(), Guid.NewGuid(), CartOperationType.Merge, Fingerprint, 1, 1, null, Now));
        AssertRule(CartRule.OperationResultDataInvalid, () => CartOperation.Create(
            Guid.NewGuid(), Guid.NewGuid(), CartOperationType.Add, Fingerprint, 1, 1, "{}", Now));
        AssertRule(CartRule.AuditInstantMustBeUtc, () => CartOperation.Create(
            Guid.NewGuid(), Guid.NewGuid(), CartOperationType.Add, Fingerprint, 1, 1, null,
            Now.ToOffset(TimeSpan.FromHours(7))));
    }

    private static void AssertRule(CartRule expected, Action action) =>
        Assert.Equal(expected, Assert.Throws<CartRuleException>(action).Rule);
}
