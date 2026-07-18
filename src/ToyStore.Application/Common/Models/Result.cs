namespace ToyStore.Application.Common.Models;

public class Result
{
    protected Result(
        bool isSuccess,
        Error error,
        IEnumerable<FieldValidationFailure>? validationFailures = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var fieldFailures = validationFailures?.ToArray() ?? [];

        if (isSuccess != (error == Error.None))
        {
            throw new ArgumentException(
                "A successful result must have no error and a failed result must have an error.",
                nameof(error));
        }

        if (isSuccess && fieldFailures.Length != 0)
        {
            throw new ArgumentException(
                "A successful result cannot have validation failures.",
                nameof(validationFailures));
        }

        if (fieldFailures.Length != 0 && error.Type != ErrorType.Validation)
        {
            throw new ArgumentException(
                "Field validation failures require a validation error.",
                nameof(validationFailures));
        }

        IsSuccess = isSuccess;
        Error = error;
        ValidationFailures = Array.AsReadOnly(fieldFailures);
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public IReadOnlyList<FieldValidationFailure> ValidationFailures { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(
        Error error,
        IEnumerable<FieldValidationFailure>? validationFailures = null) =>
        new(false, error, validationFailures);
}
