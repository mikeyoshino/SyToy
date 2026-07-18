namespace ToyStore.Application.Common.Authorization;

public static class PolicyNames
{
    public const string CanAccessAdmin = "CanAccessAdmin";
    public const string CanManageProducts = "CanManageProducts";
    public const string CanManageOrders = "CanManageOrders";
    public const string CanVerifyPayments = "CanVerifyPayments";
    public const string CanManageUsers = "CanManageUsers";
    public const string CanUseCustomerCart = "CanUseCustomerCart";
    public const string CanViewCustomerOrders = "CanViewCustomerOrders";
}
