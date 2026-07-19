namespace ToyStore.Web.Components.Admin.Primitives;

public sealed record AdminOrderFilterQueryState(
    string? Search = null,
    string SaleType = "all",
    string PaymentStatus = "all",
    string FulfillmentStatus = "all",
    string? CreatedFrom = null,
    string? CreatedTo = null,
    int Page = 1);
