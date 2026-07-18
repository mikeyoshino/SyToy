namespace ToyStore.Application.Common.Models;

public sealed class Result<T> : Result
{
    private readonly T? value;

    private Result(
        T? value,
        bool isSuccess,
        Error error,
        IEnumerable<FieldValidationFailure>? validationFailures = null)
        : base(isSuccess, error, validationFailures)
    {
        this.value = value;
    }

    public T Value => IsSuccess
        ? value!
        : throw new InvalidOperationException("The value of a failed result cannot be accessed.");

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "The approved Result<T> factory API keeps valid construction explicit.")]
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new Result<T>(value, true, Error.None);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "The approved Result<T> factory API keeps valid construction explicit.")]
    public new static Result<T> Failure(
        Error error,
        IEnumerable<FieldValidationFailure>? validationFailures = null) =>
        new(default, false, error, validationFailures);
}
