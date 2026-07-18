using ToyStore.Application.Common.Models;

namespace ToyStore.UnitTests.Application;

public sealed class ResultTests
{
    [Fact]
    public void SuccessIsSuccessfulAndHasNoError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void FailureIsFailedAndPreservesError()
    {
        var error = new Error("Products.NotFound", "Product was not found.", ErrorType.NotFound);

        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
        Assert.Empty(result.ValidationFailures);
    }

    [Fact]
    public void ValidationFailurePreservesStableFieldPathsAndThaiMessages()
    {
        var failures = new[]
        {
            new FieldValidationFailure("Address.Postcode", "กรุณากรอกรหัสไปรษณีย์"),
            new FieldValidationFailure("Email", "รูปแบบอีเมลไม่ถูกต้อง"),
        };

        var result = Result.Failure(RequestErrors.ValidationFailed, failures);

        Assert.Equal(RequestErrors.ValidationFailed, result.Error);
        Assert.Equal(failures, result.ValidationFailures);
    }

    [Fact]
    public void NonValidationFailureRejectsFieldFailures()
    {
        var error = new Error("Products.Duplicate", "มีข้อมูลนี้แล้ว", ErrorType.Conflict);

        Assert.Throws<ArgumentException>(() => Result.Failure(
            error,
            [new FieldValidationFailure("Name", "ชื่อซ้ำ")]));
    }

    [Fact]
    public void GenericSuccessExposesValue()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void GenericFailureValueThrowsInvalidOperationException()
    {
        var result = Result<int>.Failure(
            new Error("Products.NotFound", "Product was not found.", ErrorType.NotFound));

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void GenericSuccessRejectsNullReferenceValue()
    {
        Assert.Throws<ArgumentNullException>(() => Result<string?>.Success(null));
    }

    [Fact]
    public void ConstructorRejectsSuccessfulResultWithAnError()
    {
        var error = new Error("Products.Invalid", "Product is invalid.", ErrorType.Validation);

        Assert.Throws<ArgumentException>(() => new TestResult(isSuccess: true, error));
    }

    [Fact]
    public void ConstructorRejectsFailedResultWithoutAnError()
    {
        Assert.Throws<ArgumentException>(() => new TestResult(isSuccess: false, Error.None));
    }

    private sealed class TestResult(bool isSuccess, Error error) : Result(isSuccess, error);
}
