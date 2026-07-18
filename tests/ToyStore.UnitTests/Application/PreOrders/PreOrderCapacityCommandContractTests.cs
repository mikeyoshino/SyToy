using ToyStore.Application.Common.Authorization;
using ToyStore.Application.PreOrders;
using ToyStore.Application.PreOrders.ReservePreOrderCapacity;
using ToyStore.Application.PreOrders.TransitionPreOrderCapacity;

namespace ToyStore.UnitTests.Application.PreOrders;

public sealed class PreOrderCapacityCommandContractTests
{
    [Fact]
    public async Task CustomerReserveIsActorFreeAndFluentValidationUsesThaiMessages()
    {
        var command = new ReservePreOrderCapacityCommand(
            Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty,
            0, 0, " ", " ");

        Assert.Equal(PolicyNames.CanUseCustomerCart, command.RequiredPolicy);
        Assert.DoesNotContain(
            command.GetType().GetConstructors().Single().GetParameters(),
            parameter => parameter.Name!.Contains("customer", StringComparison.OrdinalIgnoreCase)
                || parameter.Name!.Contains("actor", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            command.GetType().GetConstructors().Single().GetParameters(),
            parameter => parameter.Name!.Contains("expir", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(TimeSpan.FromMinutes(32), PreOrderCapacityPolicy.ReservationLifetime);

        var validation = await new ReservePreOrderCapacityValidator().ValidateAsync(
            command,
            TestContext.Current.CancellationToken);
        Assert.NotEmpty(validation.Errors);
        Assert.All(validation.Errors, failure => Assert.Contains(
            failure.ErrorMessage,
            character => character is >= '\u0E00' and <= '\u0E7F'));
    }

    [Fact]
    public void TransitionSupportsAllCapacityLifecycleActionsAndTypedErrors()
    {
        Assert.Equal(
            [
                PreOrderCapacityAction.Consume,
                PreOrderCapacityAction.Release,
                PreOrderCapacityAction.Expire,
                PreOrderCapacityAction.CancelCustomer,
                PreOrderCapacityAction.CancelAdminOrSupplier,
                PreOrderCapacityAction.CancelBalanceOverdue,
            ],
            Enum.GetValues<PreOrderCapacityAction>());
        Assert.Equal("PreOrderCapacity.Closed", PreOrderCapacityErrors.Closed.Code);
        Assert.Equal("PreOrderCapacity.CustomerLimitExceeded", PreOrderCapacityErrors.CustomerLimitExceeded.Code);
        Assert.Equal("PreOrderCapacity.InsufficientCapacity", PreOrderCapacityErrors.InsufficientCapacity.Code);
    }
}
