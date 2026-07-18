using ToyStore.Application.Common.Models;
using ToyStore.Domain.PreOrders;

namespace ToyStore.Application.PreOrders;

internal static class PreOrderCapacityMutationSupport
{
    public static Error Map(PreOrderCapacityRuleException exception) => exception.Rule switch
    {
        PreOrderCapacityRule.PreOrderClosed => PreOrderCapacityErrors.Closed,
        PreOrderCapacityRule.InsufficientRemainingCapacity => PreOrderCapacityErrors.InsufficientCapacity,
        PreOrderCapacityRule.ConcurrencyVersionMismatch => PreOrderCapacityErrors.StaleVersion,
        PreOrderCapacityRule.ReservationTransitionInvalid => PreOrderCapacityErrors.InvalidTransition,
        PreOrderCapacityRule.ReservationEvidenceConflict => PreOrderCapacityErrors.OperationConflict,
        PreOrderCapacityRule.ReservationExpireTooEarly => PreOrderCapacityErrors.ExpireTooEarly,
        PreOrderCapacityRule.ReservationExpiryInvalid => PreOrderCapacityErrors.InvalidExpiry,
        PreOrderCapacityRule.QuantityOverflow => PreOrderCapacityErrors.QuantityOverflow,
        PreOrderCapacityRule.ConcurrencyVersionExhausted => PreOrderCapacityErrors.VersionExhausted,
        _ => throw new InvalidOperationException("Unexpected pre-order capacity domain failure.", exception),
    };
}
