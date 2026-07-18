namespace ToyStore.Application.PreOrders;

public static class PreOrderCapacityPolicy
{
    public static readonly TimeSpan ReservationLifetime = TimeSpan.FromMinutes(32);
}
