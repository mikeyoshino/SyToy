namespace ToyStore.Web.Components.Admin.Primitives;

/// <summary>
/// Immutable URL-derived filter state. The owning page recreates this value from query parameters
/// after forward or back navigation; <see cref="AdminFilterBar"/> owns only the editable form copy.
/// </summary>
public sealed record AdminFilterQueryState(
    string? Search = null,
    string Status = "active",
    int Page = 1);

public sealed class AdminFilterQueryModel
{
    public string? Search { get; set; }

    public string Status { get; set; } = "active";

    public int Page { get; set; } = 1;

    internal AdminFilterQueryState ToHistoryState() => new(Search, Status, Page);
}

internal sealed class AdminFilterEditState
{
    private AdminFilterQueryState? restoredState;

    public AdminFilterQueryModel Model { get; private set; } = new();

    public bool Restore(AdminFilterQueryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state == restoredState)
        {
            return false;
        }

        restoredState = state;
        Model = new AdminFilterQueryModel
        {
            Search = state.Search,
            Status = state.Status,
            Page = state.Page,
        };
        return true;
    }
}
