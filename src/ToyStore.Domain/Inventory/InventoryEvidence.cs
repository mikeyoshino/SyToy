namespace ToyStore.Domain.Inventory;

internal static class InventoryEvidence
{
    public static string PrepareActor(string actor) =>
        Prepare(
            actor,
            InventoryLimits.ActorLength,
            InventoryRule.ActorRequired,
            InventoryRule.ActorTooLong);

    public static string PrepareReason(string reason) =>
        Prepare(
            reason,
            InventoryLimits.ReasonLength,
            InventoryRule.ReasonRequired,
            InventoryRule.ReasonTooLong);

    public static string PrepareReference(string reference) =>
        Prepare(
            reference,
            InventoryLimits.ReferenceLength,
            InventoryRule.ReferenceRequired,
            InventoryRule.ReferenceTooLong);

    public static void EnsureUtc(DateTimeOffset instant)
    {
        if (instant.Offset != TimeSpan.Zero)
        {
            throw new InventoryRuleException(InventoryRule.AuditInstantMustBeUtc);
        }
    }

    private static string Prepare(
        string value,
        int maximumLength,
        InventoryRule requiredRule,
        InventoryRule tooLongRule)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InventoryRuleException(requiredRule);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maximumLength)
        {
            throw new InventoryRuleException(tooLongRule);
        }

        return trimmed;
    }
}
