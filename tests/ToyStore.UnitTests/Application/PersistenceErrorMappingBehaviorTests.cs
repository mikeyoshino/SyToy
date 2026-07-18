using MediatR;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;

namespace ToyStore.UnitTests.Application;

public sealed class PersistenceErrorMappingBehaviorTests
{
    [Fact]
    public async Task ExactMappedFailureUsesRequestResultFactory()
    {
        var exception = new InjectedPersistenceException();
        var failure = new PersistenceFailure(
            PersistenceFailureTarget.Brand,
            PersistenceFailureKind.DuplicateDisplayName);
        var classifier = new StubClassifier(exception, failure);
        var request = new TestRequest(PersistenceFailureTarget.Brand);
        var behavior = new PersistenceErrorMappingBehavior<TestRequest, Result<string>>(classifier);

        var result = await behavior.Handle(
            request,
            _ => throw exception,
            CancellationToken.None);

        Assert.Equal(TestRequest.MappedError, result.Error);
        Assert.Equal(1, request.CreateFailureCount);
        Assert.Same(exception, classifier.ReceivedException);
    }

    [Fact]
    public async Task FailureForAnotherTargetBubbles()
    {
        var exception = new InjectedPersistenceException();
        var failure = new PersistenceFailure(
            PersistenceFailureTarget.Universe,
            PersistenceFailureKind.DuplicateDisplayName);
        var request = new TestRequest(PersistenceFailureTarget.Brand);
        var behavior = new PersistenceErrorMappingBehavior<TestRequest, Result<string>>(
            new StubClassifier(exception, failure));

        var thrown = await Assert.ThrowsAsync<InjectedPersistenceException>(() => behavior.Handle(
            request,
            _ => throw exception,
            CancellationToken.None));

        Assert.Same(exception, thrown);
        Assert.Equal(0, request.CreateFailureCount);
    }

    [Fact]
    public async Task UnknownFailureBubbles()
    {
        var exception = new InjectedPersistenceException();
        var request = new TestRequest(PersistenceFailureTarget.Brand);
        var behavior = new PersistenceErrorMappingBehavior<TestRequest, Result<string>>(
            new StubClassifier(exception, null));

        var thrown = await Assert.ThrowsAsync<InjectedPersistenceException>(() => behavior.Handle(
            request,
            _ => throw exception,
            CancellationToken.None));

        Assert.Same(exception, thrown);
        Assert.Equal(0, request.CreateFailureCount);
    }

    [Fact]
    public async Task RequestWithoutPersistenceContractBypassesClassifier()
    {
        var exception = new InjectedPersistenceException();
        var classifier = new ThrowingClassifier();
        var behavior = new PersistenceErrorMappingBehavior<PlainRequest, string>(classifier);

        var thrown = await Assert.ThrowsAsync<InjectedPersistenceException>(() => behavior.Handle(
            new PlainRequest(),
            _ => throw exception,
            CancellationToken.None));

        Assert.Same(exception, thrown);
    }

    [Fact]
    public async Task CancellationAlwaysRethrowsWithoutClassification()
    {
        var cancellation = new OperationCanceledException("cancelled");
        var classifier = new ThrowingClassifier();
        var behavior = new PersistenceErrorMappingBehavior<TestRequest, Result<string>>(
            classifier);

        var thrown = await Assert.ThrowsAsync<OperationCanceledException>(() => behavior.Handle(
            new TestRequest(PersistenceFailureTarget.Brand),
            _ => throw cancellation,
            CancellationToken.None));

        Assert.Same(cancellation, thrown);
    }

    private sealed class TestRequest(PersistenceFailureTarget target)
        : IPersistenceFailureResultRequest<Result<string>>
    {
        public static readonly Error MappedError = new(
            "Brand.DuplicateDisplayName",
            "ชื่อแบรนด์นี้มีอยู่แล้ว",
            ErrorType.Conflict);

        public int CreateFailureCount { get; private set; }

        public Result<string> CreateFailure(
            Error requestError,
            IReadOnlyList<FieldValidationFailure>? validationFailures = null)
        {
            CreateFailureCount++;
            return Result<string>.Failure(requestError, validationFailures);
        }

        public Error? MapPersistenceFailure(PersistenceFailure failure) =>
            failure.Target == target
                && failure.Kind == PersistenceFailureKind.DuplicateDisplayName
                    ? MappedError
                    : null;
    }

    private sealed record PlainRequest : IRequest<string>;

    private sealed class StubClassifier(
        Exception expectedException,
        PersistenceFailure? failure) : IPersistenceFailureClassifier
    {
        public Exception? ReceivedException { get; private set; }

        public PersistenceFailure? Classify(Exception exception)
        {
            Assert.Same(expectedException, exception);
            ReceivedException = exception;
            return failure;
        }
    }

    private sealed class ThrowingClassifier : IPersistenceFailureClassifier
    {
        public PersistenceFailure? Classify(Exception exception) =>
            throw new InvalidOperationException("Classifier must not be called.");
    }

    private sealed class InjectedPersistenceException : Exception;
}
