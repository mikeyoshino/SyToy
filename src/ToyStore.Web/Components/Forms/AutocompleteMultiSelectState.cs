using System.Collections.ObjectModel;

namespace ToyStore.Web.Components.Forms;

internal enum AutocompleteKey
{
    ArrowDown,
    ArrowUp,
    Enter,
    Escape,
    Tab,
    Backspace,
}

internal enum AutocompleteKeyIntent
{
    None,
    Navigation,
    SelectionPublished,
    InlineCreateRequested,
    Closed,
    RemovalPublished,
}

internal enum AutocompleteActiveItem
{
    None,
    Option,
    InlineCreate,
}

internal readonly record struct AutocompleteKeyResult(
    bool Handled,
    AutocompleteKeyIntent Intent);

internal sealed class AutocompleteMultiSelectState<TOwner, TOption, TKey> : IDisposable
{
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(250);

    private readonly AutocompleteOptionContract<TOption, TKey> contract;
    private readonly Action<IReadOnlyList<TOption>> publishValues;
    private string inlineCreateId;
    private readonly IAutocompleteDelay delay;
    private readonly IEqualityComparer<TOwner> ownerComparer;
    private IReadOnlyList<TOption> values = Array.Empty<TOption>();
    private IReadOnlyList<TOption> options = Array.Empty<TOption>();
    private CancellationTokenSource? searchCancellation;
    private TOwner owner;
    private long generation;
    private int activeIndex = -1;
    private bool offerInlineCreate;
    private bool inlineCreateEnabled = true;
    private bool isComposing;
    private bool isDisposed;

    public AutocompleteMultiSelectState(
        TOwner owner,
        AutocompleteOptionContract<TOption, TKey> contract,
        Action<IReadOnlyList<TOption>> publishValues,
        string inlineCreateId,
        IAutocompleteDelay? delay = null,
        IEqualityComparer<TOwner>? ownerComparer = null)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(publishValues);
        ArgumentException.ThrowIfNullOrWhiteSpace(inlineCreateId);

        this.owner = owner;
        this.contract = contract;
        this.publishValues = publishValues;
        this.inlineCreateId = inlineCreateId;
        this.delay = delay ?? AutocompleteDelay.Instance;
        this.ownerComparer = ownerComparer ?? EqualityComparer<TOwner>.Default;
    }

    public IReadOnlyList<TOption> Values => values;

    public IReadOnlyList<TOption> Options => options;

    public string Term { get; private set; } = string.Empty;

    public AutocompleteFailure? Error { get; private set; }

    public bool IsLoading { get; private set; }

    public bool IsCreating { get; private set; }

    public bool IsOpen { get; private set; }

    public bool CanOfferInlineCreate =>
        inlineCreateEnabled
        && offerInlineCreate
        && !string.IsNullOrWhiteSpace(Term)
        && !values.Any(value => string.Equals(
            contract.Label(value),
            Term,
            StringComparison.Ordinal));

    public AutocompleteActiveItem ActiveItem
    {
        get
        {
            if (activeIndex < 0)
            {
                return AutocompleteActiveItem.None;
            }

            return activeIndex < options.Count
                ? AutocompleteActiveItem.Option
                : AutocompleteActiveItem.InlineCreate;
        }
    }

    public string? ActiveDescendantId => ActiveItem switch
    {
        AutocompleteActiveItem.Option => contract.Id(options[activeIndex]),
        AutocompleteActiveItem.InlineCreate => inlineCreateId,
        _ => null,
    };

    public void SetValues(IEnumerable<TOption> incomingValues)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(incomingValues);

        values = SnapshotDistinct(incomingValues);
        options = SnapshotAvailable(options);
        EnsureActiveIndexIsValid();
    }

    public void SetInlineCreateId(string value)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        inlineCreateId = value;
    }

    public void SetInlineCreateEnabled(bool value)
    {
        ThrowIfDisposed();
        inlineCreateEnabled = value;
        EnsureActiveIndexIsValid();
    }

    public void CloseFromBlur()
    {
        ThrowIfDisposed();
        CancelPendingAndClose();
    }

    public void SetTerm(string term)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(term);

        CancelPendingSearch();
        Term = term;
        ResetSearchPresentation(close: false);
    }

    public bool Select(TOption option)
    {
        ThrowIfDisposed();
        if (ContainsKey(values, contract.Key(option)))
        {
            return false;
        }

        Publish(values.Append(option));
        CloseAndClearSearch();
        return true;
    }

    public bool Remove(TOption option)
    {
        ThrowIfDisposed();
        var key = contract.Key(option);
        if (!ContainsKey(values, key))
        {
            return false;
        }

        Publish(values.Where(value => !contract.KeyComparer.Equals(contract.Key(value), key)));
        return true;
    }

    public void Open()
    {
        ThrowIfDisposed();
        IsOpen = true;
    }

    public AutocompleteKeyResult HandleKey(AutocompleteKey key)
    {
        ThrowIfDisposed();
        if (isComposing)
        {
            return None();
        }

        return key switch
        {
            AutocompleteKey.ArrowDown => Navigate(forward: true),
            AutocompleteKey.ArrowUp => Navigate(forward: false),
            AutocompleteKey.Enter => Activate(),
            AutocompleteKey.Escape => Close(),
            AutocompleteKey.Tab => ReleaseFocus(),
            AutocompleteKey.Backspace => RemoveLastWhenEmpty(),
            _ => None(),
        };
    }

    public async Task SearchAsync(
        string term,
        Func<TOwner, string, CancellationToken, Task<AutocompleteSearchResult<TOption>>> search)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(term);
        ArgumentNullException.ThrowIfNull(search);

        CancelPendingSearch();
        Term = term;
        ResetSearchPresentation(close: false);
        IsOpen = true;

        if (isComposing)
        {
            return;
        }

        var localGeneration = generation;
        var localOwner = owner;
        var cancellation = new CancellationTokenSource();
        searchCancellation = cancellation;
        var cancellationToken = cancellation.Token;
        IsLoading = true;

        try
        {
            await delay.DelayAsync(SearchDelay, cancellationToken);
            var result = await search(localOwner, term, cancellationToken);
            if (!IsCurrent(localGeneration, localOwner, cancellationToken))
            {
                return;
            }

            ApplySearchResult(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (IsCurrent(localGeneration, localOwner, cancellationToken))
            {
                IsLoading = false;
                searchCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    public bool SetOwner(TOwner newOwner)
    {
        ThrowIfDisposed();
        if (ownerComparer.Equals(owner, newOwner))
        {
            return false;
        }

        CancelPendingSearch();
        owner = newOwner;
        Term = string.Empty;
        ResetSearchPresentation(close: true);
        IsCreating = false;
        isComposing = false;
        Publish(Array.Empty<TOption>());
        return true;
    }

    public bool BeginInlineCreate()
    {
        ThrowIfDisposed();
        if (!CanOfferInlineCreate || IsCreating || IsLoading)
        {
            return false;
        }

        Error = null;
        IsCreating = true;
        return true;
    }

    public void FailInlineCreate(AutocompleteFailure failure)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(failure);

        IsCreating = false;
        Error = failure;
        IsOpen = true;
    }

    public bool CompleteInlineCreate(TOption option)
    {
        ThrowIfDisposed();
        if (!IsCreating)
        {
            return false;
        }

        IsCreating = false;
        return Select(option);
    }

    public void BeginComposition()
    {
        ThrowIfDisposed();
        isComposing = true;
    }

    public Task EndCompositionAsync(
        Func<TOwner, string, CancellationToken, Task<AutocompleteSearchResult<TOption>>> search)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(search);

        isComposing = false;
        return SearchAsync(Term, search);
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        CancelPendingSearch();
        IsLoading = false;
        IsCreating = false;
    }

    private void ApplySearchResult(AutocompleteSearchResult<TOption> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Error = result.Failure;
        if (result.Failure is not null)
        {
            options = Array.Empty<TOption>();
            offerInlineCreate = false;
        }
        else
        {
            options = SnapshotAvailable(result.Items);
            offerInlineCreate = result.OfferInlineCreate;
        }

        activeIndex = -1;
        IsOpen = true;
    }

    private AutocompleteKeyResult Navigate(bool forward)
    {
        IsOpen = true;
        var itemCount = options.Count + (CanOfferInlineCreate ? 1 : 0);
        if (itemCount == 0)
        {
            activeIndex = -1;
            return new(true, AutocompleteKeyIntent.Navigation);
        }

        activeIndex = forward
            ? (activeIndex + 1) % itemCount
            : activeIndex <= 0 ? itemCount - 1 : activeIndex - 1;
        return new(true, AutocompleteKeyIntent.Navigation);
    }

    private AutocompleteKeyResult Activate()
    {
        if (!IsOpen || activeIndex < 0)
        {
            return None();
        }

        if (activeIndex < options.Count)
        {
            return Select(options[activeIndex])
                ? new(true, AutocompleteKeyIntent.SelectionPublished)
                : None();
        }

        return CanOfferInlineCreate
            ? new(true, AutocompleteKeyIntent.InlineCreateRequested)
            : None();
    }

    private AutocompleteKeyResult Close()
    {
        CancelPendingAndClose();
        return new(true, AutocompleteKeyIntent.Closed);
    }

    private void CancelPendingAndClose()
    {
        CancelPendingSearch();
        IsLoading = false;
        IsOpen = false;
        activeIndex = -1;
    }

    private AutocompleteKeyResult ReleaseFocus()
    {
        CancelPendingAndClose();
        return None();
    }

    private AutocompleteKeyResult RemoveLastWhenEmpty()
    {
        if (Term.Length != 0 || values.Count == 0)
        {
            return None();
        }

        Publish(values.Take(values.Count - 1));
        return new(true, AutocompleteKeyIntent.RemovalPublished);
    }

    private void Publish(IEnumerable<TOption> nextValues)
    {
        publishValues(SnapshotDistinct(nextValues));
    }

    private ReadOnlyCollection<TOption> SnapshotAvailable(IEnumerable<TOption> candidates)
    {
        var selectedKeys = new HashSet<TKey>(values.Select(contract.Key), contract.KeyComparer);
        var optionKeys = new HashSet<TKey>(contract.KeyComparer);
        return Snapshot(candidates.Where(option =>
            !selectedKeys.Contains(contract.Key(option))
            && optionKeys.Add(contract.Key(option))));
    }

    private ReadOnlyCollection<TOption> SnapshotDistinct(IEnumerable<TOption> candidates)
    {
        var keys = new HashSet<TKey>(contract.KeyComparer);
        return Snapshot(candidates.Where(option => keys.Add(contract.Key(option))));
    }

    private static ReadOnlyCollection<TOption> Snapshot(IEnumerable<TOption> items) =>
        new ReadOnlyCollection<TOption>(items.ToArray());

    private bool ContainsKey(IEnumerable<TOption> items, TKey key) =>
        items.Any(item => contract.KeyComparer.Equals(contract.Key(item), key));

    private void ResetSearchPresentation(bool close)
    {
        options = Array.Empty<TOption>();
        Error = null;
        offerInlineCreate = false;
        activeIndex = -1;
        IsLoading = false;
        if (close)
        {
            IsOpen = false;
        }
    }

    private void CloseAndClearSearch()
    {
        CancelPendingSearch();
        ResetSearchPresentation(close: true);
    }

    private void EnsureActiveIndexIsValid()
    {
        var itemCount = options.Count + (CanOfferInlineCreate ? 1 : 0);
        if (activeIndex >= itemCount)
        {
            activeIndex = -1;
        }
    }

    private bool IsCurrent(
        long localGeneration,
        TOwner localOwner,
        CancellationToken cancellationToken) =>
        !isDisposed
        && !cancellationToken.IsCancellationRequested
        && localGeneration == generation
        && ownerComparer.Equals(owner, localOwner);

    private void CancelPendingSearch()
    {
        generation++;
        var cancellation = searchCancellation;
        searchCancellation = null;
        cancellation?.Cancel();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
    }

    private static AutocompleteKeyResult None() =>
        new(false, AutocompleteKeyIntent.None);
}
