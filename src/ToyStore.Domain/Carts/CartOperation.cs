namespace ToyStore.Domain.Carts;

public sealed class CartOperation
{
    public const int FingerprintLength = 64;

    private CartOperation()
    {
        IntentFingerprint = null!;
    }

    private CartOperation(
        Guid id,
        Guid cartId,
        CartOperationType type,
        string intentFingerprint,
        long resultingCartVersion,
        long resultingTotalQuantity,
        string? resultData,
        DateTimeOffset occurredAtUtc)
    {
        Id = id;
        CartId = cartId;
        Type = type;
        IntentFingerprint = intentFingerprint;
        ResultingCartVersion = resultingCartVersion;
        ResultingTotalQuantity = resultingTotalQuantity;
        ResultData = resultData;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid CartId { get; private set; }

    public CartOperationType Type { get; private set; }

    public string IntentFingerprint { get; private set; }

    public long ResultingCartVersion { get; private set; }

    public long ResultingTotalQuantity { get; private set; }

    public string? ResultData { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public static CartOperation Create(
        Guid id,
        Guid cartId,
        CartOperationType type,
        string intentFingerprint,
        long resultingCartVersion,
        long resultingTotalQuantity,
        string? resultData,
        DateTimeOffset occurredAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new CartRuleException(CartRule.OperationIdentityRequired);
        }

        if (cartId == Guid.Empty)
        {
            throw new CartRuleException(CartRule.CartIdentityRequired);
        }

        if (!Enum.IsDefined(type))
        {
            throw new CartRuleException(CartRule.OperationTypeInvalid);
        }

        if (intentFingerprint is null
            || intentFingerprint.Length != FingerprintLength
            || intentFingerprint.Any(character => character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            throw new CartRuleException(CartRule.OperationFingerprintInvalid);
        }

        if (resultingCartVersion <= 0)
        {
            throw new CartRuleException(CartRule.OperationResultVersionInvalid);
        }

        if (resultingTotalQuantity < 0)
        {
            throw new CartRuleException(CartRule.OperationResultTotalInvalid);
        }

        var mergeRequiresResultData = type == CartOperationType.Merge;
        if ((mergeRequiresResultData && string.IsNullOrWhiteSpace(resultData))
            || (!mergeRequiresResultData && resultData is not null))
        {
            throw new CartRuleException(CartRule.OperationResultDataInvalid);
        }

        CartRules.EnsureUtc(occurredAtUtc);
        return new CartOperation(
            id,
            cartId,
            type,
            intentFingerprint,
            resultingCartVersion,
            resultingTotalQuantity,
            resultData,
            occurredAtUtc);
    }
}
