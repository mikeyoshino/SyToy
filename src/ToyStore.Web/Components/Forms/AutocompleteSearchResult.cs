using System.Collections.ObjectModel;

namespace ToyStore.Web.Components.Forms;

public enum AutocompleteFailureKind
{
    Validation,
    Business,
    System,
}

public sealed record AutocompleteFailure(
    AutocompleteFailureKind Kind,
    string Message);

public sealed class AutocompleteSearchResult<TOption>
{
    private AutocompleteSearchResult(
        IReadOnlyList<TOption> items,
        bool offerInlineCreate,
        AutocompleteFailure? failure)
    {
        Items = items;
        OfferInlineCreate = offerInlineCreate;
        Failure = failure;
    }

    public IReadOnlyList<TOption> Items { get; }

    public bool OfferInlineCreate { get; }

    public AutocompleteFailure? Failure { get; }

    internal static AutocompleteSearchResult<TOption> CreateSuccess(
        IEnumerable<TOption> items,
        bool offerInlineCreate)
    {
        ArgumentNullException.ThrowIfNull(items);

        return new(
            new ReadOnlyCollection<TOption>(items.ToArray()),
            offerInlineCreate,
            failure: null);
    }

    internal static AutocompleteSearchResult<TOption> CreateFailure(AutocompleteFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        return new(
            Array.Empty<TOption>(),
            offerInlineCreate: false,
            failure);
    }
}

public static class AutocompleteSearchResults
{
    public static AutocompleteSearchResult<TOption> Success<TOption>(
        IEnumerable<TOption> items,
        bool offerInlineCreate) =>
        AutocompleteSearchResult<TOption>.CreateSuccess(items, offerInlineCreate);

    public static AutocompleteSearchResult<TOption> Failed<TOption>(
        AutocompleteFailure failure) =>
        AutocompleteSearchResult<TOption>.CreateFailure(failure);
}

internal interface IAutocompleteDelay
{
    Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken);
}

internal sealed class AutocompleteDelay : IAutocompleteDelay
{
    public static AutocompleteDelay Instance { get; } = new();

    private AutocompleteDelay()
    {
    }

    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken) =>
        Task.Delay(duration, cancellationToken);
}
