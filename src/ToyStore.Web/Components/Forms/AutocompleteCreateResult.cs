namespace ToyStore.Web.Components.Forms;

public sealed class AutocompleteCreateResult<TOption>
{
    private AutocompleteCreateResult(TOption? option, AutocompleteFailure? failure)
    {
        Option = option;
        Failure = failure;
    }

    public TOption? Option { get; }

    public AutocompleteFailure? Failure { get; }

    public bool IsSuccess => Failure is null;

    internal static AutocompleteCreateResult<TOption> CreateSuccess(TOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        return new(option, failure: null);
    }

    internal static AutocompleteCreateResult<TOption> CreateFailure(AutocompleteFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new(default, failure);
    }
}

public static class AutocompleteCreateResults
{
    public static AutocompleteCreateResult<TOption> Success<TOption>(TOption option) =>
        AutocompleteCreateResult<TOption>.CreateSuccess(option);

    public static AutocompleteCreateResult<TOption> Failed<TOption>(
        AutocompleteFailure failure) =>
        AutocompleteCreateResult<TOption>.CreateFailure(failure);
}
