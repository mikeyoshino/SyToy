namespace ToyStore.Application.Common.Authorization;

public static class RoleNames
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";

    public static IReadOnlyList<string> All { get; } =
        Array.AsReadOnly<string>([Customer, Admin]);
}
