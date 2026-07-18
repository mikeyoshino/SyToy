namespace ToyStore.Web.Components.Admin.Navigation;

public sealed record AdminNavigationItem(
    string Label,
    string Href,
    AdminIconName Icon,
    string RouteGroup,
    int? ActionableCount = null);

public sealed record AdminContextNavigationItem(string Label, string Href);
