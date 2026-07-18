namespace ToyStore.Domain.PreOrders;

internal static class PreOrderCapacityEvidence
{
    public static string PrepareActor(string actor) =>
        Prepare(
            actor,
            PreOrderCapacityLimits.ActorLength,
            PreOrderCapacityRule.ActorRequired,
            PreOrderCapacityRule.ActorTooLong);

    public static string PrepareCustomerId(string customerId) =>
        Prepare(
            customerId,
            PreOrderCapacityLimits.CustomerIdLength,
            PreOrderCapacityRule.CustomerIdentityRequired,
            PreOrderCapacityRule.CustomerTooLong);

    public static string PrepareReason(string reason) =>
        Prepare(
            reason,
            PreOrderCapacityLimits.ReasonLength,
            PreOrderCapacityRule.ReasonRequired,
            PreOrderCapacityRule.ReasonTooLong);

    public static string PrepareReference(string reference) =>
        Prepare(
            reference,
            PreOrderCapacityLimits.ReferenceLength,
            PreOrderCapacityRule.ReferenceRequired,
            PreOrderCapacityRule.ReferenceTooLong);

    public static void EnsureUtc(DateTimeOffset instant)
    {
        if (instant.Offset != TimeSpan.Zero)
        {
            throw new PreOrderCapacityRuleException(
                PreOrderCapacityRule.AuditInstantMustBeUtc);
        }
    }

    private static string Prepare(
        string value,
        int maximumLength,
        PreOrderCapacityRule requiredRule,
        PreOrderCapacityRule tooLongRule)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new PreOrderCapacityRuleException(requiredRule);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maximumLength)
        {
            throw new PreOrderCapacityRuleException(tooLongRule);
        }

        return trimmed;
    }
}
