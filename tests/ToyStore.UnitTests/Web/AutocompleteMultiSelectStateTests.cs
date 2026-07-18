using ToyStore.Web.Components.Forms;

namespace ToyStore.UnitTests.Web;

public sealed class AutocompleteMultiSelectStateTests
{
    private static readonly AutocompleteOptionContract<Option, int> Contract = new(
        option => option.Id,
        option => option.Label,
        option => $"option-{option.Id}");

    [Fact]
    public void ControlledValuesPreventDuplicatesPreserveOrderAndPublishImmutableSnapshots()
    {
        var published = new List<IReadOnlyList<Option>>();
        using var state = State("owner-a", published);
        var first = new Option(1, "หนึ่ง");
        var second = new Option(2, "สอง");
        var third = new Option(3, "สาม");

        state.SetValues([first, second, first]);

        Assert.Equal([first, second], state.Values);
        Assert.False(state.Select(first));
        Assert.True(state.Select(third));
        Assert.Equal([first, second], state.Values);
        Assert.Equal([first, second, third], Assert.Single(published));
        Assert.Throws<NotSupportedException>(
            () => ((IList<Option>)published[0]).Add(new Option(4, "สี่")));

        state.SetValues(published[0]);
        Assert.True(state.Remove(second));
        Assert.Equal([first, third], published[1]);
        state.SetValues(published[1]);
        state.SetTerm(string.Empty);
        Assert.Equal(
            AutocompleteKeyIntent.RemovalPublished,
            state.HandleKey(AutocompleteKey.Backspace).Intent);
        Assert.Equal([first], published[2]);
    }

    [Fact]
    public async Task NavigationWrapsAcrossOptionsAndInlineCreateAndEnterActsExactlyOnce()
    {
        var published = new List<IReadOnlyList<Option>>();
        using var state = State("owner-a", published);
        await state.SearchAsync(
            "ใหม่",
            (_, _, _) => Task.FromResult(AutocompleteSearchResults.Success<Option>(
                [new(1, "หนึ่ง"), new(2, "สอง")],
                offerInlineCreate: true)));

        Assert.Equal(AutocompleteKeyIntent.Navigation, state.HandleKey(AutocompleteKey.ArrowDown).Intent);
        Assert.Equal("option-1", state.ActiveDescendantId);
        state.HandleKey(AutocompleteKey.ArrowUp);
        Assert.Equal(AutocompleteActiveItem.InlineCreate, state.ActiveItem);
        Assert.Equal("inline-create", state.ActiveDescendantId);
        Assert.Equal(
            AutocompleteKeyIntent.InlineCreateRequested,
            state.HandleKey(AutocompleteKey.Enter).Intent);

        state.HandleKey(AutocompleteKey.ArrowDown);
        Assert.Equal(AutocompleteActiveItem.Option, state.ActiveItem);
        Assert.Equal(
            AutocompleteKeyIntent.SelectionPublished,
            state.HandleKey(AutocompleteKey.Enter).Intent);
        Assert.Equal(new Option(1, "หนึ่ง"), Assert.Single(Assert.Single(published)));
        Assert.False(state.IsOpen);
        Assert.Equal(AutocompleteKeyIntent.None, state.HandleKey(AutocompleteKey.Enter).Intent);
    }

    [Fact]
    public async Task InlineCreateActiveDescendantUsesTheCallerOwnedInstanceId()
    {
        using var first = State("owner-a", [], inlineCreateId: "characters-a-inline-create");
        using var second = State("owner-b", [], inlineCreateId: "characters-b-inline-create");
        await Search(first, "หนึ่ง", offer: true);
        await Search(second, "สอง", offer: true);

        first.HandleKey(AutocompleteKey.ArrowDown);
        second.HandleKey(AutocompleteKey.ArrowDown);

        Assert.Equal("characters-a-inline-create", first.ActiveDescendantId);
        Assert.Equal("characters-b-inline-create", second.ActiveDescendantId);
        Assert.NotEqual(first.ActiveDescendantId, second.ActiveDescendantId);
        Assert.Throws<ArgumentException>(() =>
            new AutocompleteMultiSelectState<string, Option, int>(
                "owner",
                Contract,
                _ => { },
                "   "));
    }

    [Fact]
    public async Task InlineCreateActiveDescendantTracksAChangedCallerOwnedInstanceId()
    {
        using var state = State("owner-a", [], inlineCreateId: "characters-old-inline-create");
        await Search(state, "ใหม่", offer: true);
        state.HandleKey(AutocompleteKey.ArrowDown);

        state.SetInlineCreateId("characters-new-inline-create");

        Assert.Equal("characters-new-inline-create", state.ActiveDescendantId);
        Assert.Throws<ArgumentException>(() => state.SetInlineCreateId(" "));
    }

    [Fact]
    public async Task InlineCreateRequiresAuthoritativeOfferNonblankTermAndNoSelectedExactLabel()
    {
        using var state = State("owner-a", []);
        state.SetValues([new(1, "มีแล้ว")]);

        await Search(state, "   ", offer: true);
        Assert.False(state.CanOfferInlineCreate);
        await Search(state, "มีแล้ว", offer: true);
        Assert.False(state.CanOfferInlineCreate);
        await Search(state, " มีแล้ว ", offer: true);
        Assert.True(state.CanOfferInlineCreate);
        await Search(state, "ชื่อจากเซิร์ฟเวอร์", offer: false);
        Assert.False(state.CanOfferInlineCreate);
        await Search(state, "สร้างได้", offer: true);
        Assert.True(state.CanOfferInlineCreate);
    }

    [Fact]
    public async Task MissingInlineCreateCapabilitySuppressesPseudoOptionAndKeyboardActivation()
    {
        using var state = State("owner-a", []);
        state.SetInlineCreateEnabled(false);

        await Search(state, "สร้างไม่ได้", offer: true);
        var navigation = state.HandleKey(AutocompleteKey.ArrowDown);
        var activation = state.HandleKey(AutocompleteKey.Enter);

        Assert.False(state.CanOfferInlineCreate);
        Assert.Equal(AutocompleteActiveItem.None, state.ActiveItem);
        Assert.Null(state.ActiveDescendantId);
        Assert.Equal(AutocompleteKeyIntent.Navigation, navigation.Intent);
        Assert.Equal(AutocompleteKeyIntent.None, activation.Intent);
    }

    [Fact]
    public async Task OwnerChangeCancelsInvalidatesResetsAndPublishesOneEmptyControlledSnapshot()
    {
        var delay = new ControlledDelay();
        var published = new List<IReadOnlyList<Option>>();
        using var state = State("owner-a", published, delay);
        state.SetValues([new(1, "เดิม")]);
        var oldSearch = state.SearchAsync(
            "เก่า",
            (_, _, _) => Task.FromResult(AutocompleteSearchResults.Success<Option>(
                [new(2, "ผลเก่า")], false)));

        Assert.True(state.SetOwner("owner-b"));
        Assert.True(delay.Requests[0].Token.IsCancellationRequested);
        Assert.Equal([new Option(1, "เดิม")], state.Values);
        var ownerReset = Assert.Single(published);
        Assert.Empty(ownerReset);
        Assert.Throws<NotSupportedException>(
            () => ((IList<Option>)ownerReset).Add(new Option(3, "แก้ไม่ได้")));
        Assert.Empty(state.Options);
        Assert.Equal(string.Empty, state.Term);
        Assert.Null(state.Error);
        Assert.False(state.IsLoading);
        Assert.False(state.IsCreating);
        Assert.False(state.IsOpen);
        Assert.False(state.SetOwner("owner-b"));
        Assert.Single(published);
        await oldSearch;
        Assert.Empty(state.Options);
    }

    [Fact]
    public async Task OldOwnerNonCooperativeCompletionCannotReplaceTheResetState()
    {
        var completion = new TaskCompletionSource<AutocompleteSearchResult<Option>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var state = State("owner-a", []);
        var search = state.SearchAsync(
            "เก่า",
            (owner, _, _) =>
            {
                Assert.Equal("owner-a", owner);
                return completion.Task;
            });

        Assert.True(state.SetOwner("owner-b"));
        completion.SetResult(AutocompleteSearchResults.Success<Option>([new(1, "ผลเก่า")], true));
        await search;

        Assert.Empty(state.Options);
        Assert.Equal(string.Empty, state.Term);
        Assert.False(state.CanOfferInlineCreate);
    }

    [Fact]
    public async Task SearchUsesExactlyTheDeterministicTwoHundredFiftyMillisecondDelay()
    {
        var delay = new ControlledDelay();
        using var state = State("owner-a", [], delay);
        var searchCalls = 0;
        var search = state.SearchAsync(
            "term",
            (_, _, _) =>
            {
                searchCalls++;
                return Task.FromResult(AutocompleteSearchResults.Success<Option>([], false));
            });

        Assert.True(state.IsLoading);
        Assert.Equal(TimeSpan.FromMilliseconds(250), Assert.Single(delay.Requests).Duration);
        Assert.Equal(0, searchCalls);
        delay.Release(0);
        await search;
        Assert.Equal(1, searchCalls);
        Assert.False(state.IsLoading);
    }

    [Fact]
    public async Task NewTermAndOwnerGenerationIgnoreAStaleNonCooperativeCompletion()
    {
        var first = new TaskCompletionSource<AutocompleteSearchResult<Option>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<AutocompleteSearchResult<Option>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var state = State("owner-a", []);

        var oldSearch = state.SearchAsync("old", (_, _, _) => first.Task);
        var newSearch = state.SearchAsync("new", (_, _, _) => second.Task);
        second.SetResult(AutocompleteSearchResults.Success<Option>([new(2, "ใหม่")], false));
        await newSearch;
        first.SetResult(AutocompleteSearchResults.Success<Option>([new(1, "เก่า")], false));
        await oldSearch;

        Assert.Equal([new Option(2, "ใหม่")], state.Options);
        Assert.Equal("new", state.Term);
    }

    [Fact]
    public async Task TermChangeAndDisposalCancelPendingDebounceAndSearch()
    {
        var delay = new ControlledDelay();
        var state = State("owner-a", [], delay);
        var first = state.SearchAsync("one", (_, _, _) =>
            Task.FromResult(AutocompleteSearchResults.Success<Option>([], false)));
        var firstToken = delay.Requests[0].Token;

        var second = state.SearchAsync("two", (_, _, _) =>
            Task.FromResult(AutocompleteSearchResults.Success<Option>([], false)));
        Assert.True(firstToken.IsCancellationRequested);
        var secondToken = delay.Requests[1].Token;
        state.Dispose();
        Assert.True(secondToken.IsCancellationRequested);
        await Task.WhenAll(first, second);
    }

    [Fact]
    public async Task SearchLoadingSuccessEmptyAndEveryFailureClearStaleOptions()
    {
        using var state = State("owner-a", []);
        await Search(state, "ok", [new(1, "ผล")]);
        Assert.Single(state.Options);
        Assert.True(state.IsOpen);

        await Search(state, "empty", []);
        Assert.Empty(state.Options);
        Assert.Null(state.Error);

        foreach (var kind in Enum.GetValues<AutocompleteFailureKind>())
        {
            await state.SearchAsync(
                "failed",
                (_, _, _) => Task.FromResult(
                    AutocompleteSearchResults.Failed<Option>(new(kind, $"error-{kind}"))));
            Assert.Empty(state.Options);
            Assert.Equal(kind, state.Error?.Kind);
            Assert.False(state.IsLoading);
            Assert.False(state.CanOfferInlineCreate);
        }
    }

    [Fact]
    public async Task BusyInlineCreateBlocksDoubleSubmitAndFailureOrSuccessEndsBusyState()
    {
        var published = new List<IReadOnlyList<Option>>();
        using var state = State("owner-a", published);
        await Search(state, "สร้าง", offer: true);

        Assert.True(state.BeginInlineCreate());
        Assert.False(state.BeginInlineCreate());
        Assert.True(state.IsCreating);
        state.FailInlineCreate(new(AutocompleteFailureKind.Business, "ชื่อซ้ำ"));
        Assert.False(state.IsCreating);
        Assert.Equal("ชื่อซ้ำ", state.Error?.Message);
        Assert.Empty(published);

        Assert.True(state.BeginInlineCreate());
        Assert.True(state.CompleteInlineCreate(new(9, "สร้าง")));
        Assert.False(state.IsCreating);
        Assert.False(state.IsOpen);
        Assert.Equal(new Option(9, "สร้าง"), Assert.Single(Assert.Single(published)));
        Assert.False(state.CompleteInlineCreate(new(9, "สร้าง")));
    }

    [Fact]
    public async Task ImeCompositionSuppressesShortcutsAndDefersSearchUntilCompositionEnds()
    {
        var published = new List<IReadOnlyList<Option>>();
        using var state = State("owner-a", published);
        state.SetValues([new(1, "เดิม")]);
        state.Open();
        state.BeginComposition();
        var calls = 0;

        foreach (var key in new[]
                 {
                     AutocompleteKey.ArrowDown,
                     AutocompleteKey.ArrowUp,
                     AutocompleteKey.Enter,
                     AutocompleteKey.Escape,
                     AutocompleteKey.Backspace,
                 })
        {
            Assert.False(state.HandleKey(key).Handled);
        }

        await state.SearchAsync(
            "に",
            (_, _, _) =>
            {
                calls++;
                return Task.FromResult(AutocompleteSearchResults.Success<Option>([], false));
            });
        Assert.Equal(0, calls);
        Assert.True(state.IsOpen);
        Assert.Empty(published);

        await state.EndCompositionAsync(
            (_, _, _) =>
            {
                calls++;
                return Task.FromResult(AutocompleteSearchResults.Success<Option>([], false));
            });
        Assert.Equal(1, calls);
    }

    [Fact]
    public void EscapeClosesWhileTabNeverTrapsFocus()
    {
        using var state = State("owner-a", []);
        state.Open();
        var tab = state.HandleKey(AutocompleteKey.Tab);
        Assert.False(tab.Handled);
        Assert.False(state.IsOpen);

        state.Open();
        var escape = state.HandleKey(AutocompleteKey.Escape);
        Assert.True(escape.Handled);
        Assert.Equal(AutocompleteKeyIntent.Closed, escape.Intent);
        Assert.False(state.IsOpen);
    }

    [Fact]
    public void BlurClosesEvenWhenImeCompositionIsActive()
    {
        using var state = State("owner-a", []);
        state.Open();
        state.BeginComposition();

        state.CloseFromBlur();

        Assert.False(state.IsOpen);
        Assert.Null(state.ActiveDescendantId);
    }

    [Fact]
    public async Task BlurCancelsPendingSearchAndAStaleCompletionCannotReopenTheListbox()
    {
        var delay = new ControlledDelay();
        using var state = State("owner-a", [], delay);
        var search = state.SearchAsync(
            "กำลังค้นหา",
            (_, _, _) => Task.FromResult(
                AutocompleteSearchResults.Success<Option>([new(1, "ผลลัพธ์")], false)));
        var request = Assert.Single(delay.Requests);

        state.CloseFromBlur();

        Assert.True(request.Token.IsCancellationRequested);
        Assert.False(state.IsLoading);
        Assert.False(state.IsOpen);
        await search;
        Assert.Empty(state.Options);
        Assert.False(state.IsOpen);
    }

    [Fact]
    public async Task EscapeCancelsPendingSearchAndAStaleCompletionCannotReopenTheListbox()
    {
        var delay = new ControlledDelay();
        using var state = State("owner-a", [], delay);
        var search = state.SearchAsync(
            "กำลังค้นหา",
            (_, _, _) => Task.FromResult(
                AutocompleteSearchResults.Success<Option>([new(1, "ผลลัพธ์")], false)));
        var request = Assert.Single(delay.Requests);

        var escape = state.HandleKey(AutocompleteKey.Escape);

        Assert.True(escape.Handled);
        Assert.Equal(AutocompleteKeyIntent.Closed, escape.Intent);
        Assert.True(request.Token.IsCancellationRequested);
        Assert.False(state.IsLoading);
        Assert.False(state.IsOpen);
        await search;
        Assert.Empty(state.Options);
        Assert.False(state.IsOpen);
    }

    [Fact]
    public async Task TabReleasesFocusIntentAndCancelsPendingSearchWithoutReopening()
    {
        var delay = new ControlledDelay();
        using var state = State("owner-a", [], delay);
        var search = state.SearchAsync(
            "กำลังค้นหา",
            (_, _, _) => Task.FromResult(
                AutocompleteSearchResults.Success<Option>([new(1, "ผลลัพธ์")], false)));
        var request = Assert.Single(delay.Requests);

        var tab = state.HandleKey(AutocompleteKey.Tab);

        Assert.False(tab.Handled);
        Assert.True(request.Token.IsCancellationRequested);
        Assert.False(state.IsLoading);
        Assert.False(state.IsOpen);
        await search;
        Assert.Empty(state.Options);
        Assert.False(state.IsOpen);
    }

    private static AutocompleteMultiSelectState<string, Option, int> State(
        string owner,
        ICollection<IReadOnlyList<Option>> published,
        IAutocompleteDelay? delay = null,
        string inlineCreateId = "inline-create") =>
        new(
            owner,
            Contract,
            published.Add,
            inlineCreateId,
            delay ?? ImmediateDelay.Instance);

    private static Task Search(
        AutocompleteMultiSelectState<string, Option, int> state,
        string term,
        bool offer) =>
        state.SearchAsync(
            term,
            (_, _, _) => Task.FromResult(
                AutocompleteSearchResults.Success<Option>([], offer)));

    private static Task Search(
        AutocompleteMultiSelectState<string, Option, int> state,
        string term,
        IReadOnlyList<Option> options) =>
        state.SearchAsync(
            term,
            (_, _, _) => Task.FromResult(
                AutocompleteSearchResults.Success<Option>(options, false)));

    private sealed record Option(int Id, string Label);

    private sealed class ImmediateDelay : IAutocompleteDelay
    {
        public static ImmediateDelay Instance { get; } = new();

        public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class ControlledDelay : IAutocompleteDelay
    {
        public List<Request> Requests { get; } = [];

        public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var registration = cancellationToken.Register(
                () => completion.TrySetCanceled(cancellationToken));
            _ = completion.Task.ContinueWith(
                _ => registration.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            Requests.Add(new Request(duration, completion, cancellationToken));
            return completion.Task;
        }

        public void Release(int index) => Requests[index].Completion.TrySetResult();

        public sealed record Request(
            TimeSpan Duration,
            TaskCompletionSource Completion,
            CancellationToken Token);
    }
}
