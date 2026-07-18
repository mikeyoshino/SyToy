namespace ToyStore.Application.PreOrders.TransitionPreOrderCapacity;

public enum PreOrderCapacityAction
{
    Consume,
    Release,
    Expire,
    CancelCustomer,
    CancelAdminOrSupplier,
    CancelBalanceOverdue,
}
