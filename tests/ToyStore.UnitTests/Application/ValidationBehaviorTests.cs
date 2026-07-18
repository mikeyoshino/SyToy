using FluentValidation;
using MediatR;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Messaging;
using ToyStore.Application.Common.Models;

namespace ToyStore.UnitTests.Application;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task InvalidRequestAggregatesFailuresAndDoesNotCallHandler()
    {
        var validators = new IValidator<TestRequest>[]
        {
            new NameValidator(),
            new QuantityValidator(),
        };
        var behavior = new ValidationBehavior<TestRequest, Result<string>>(validators);
        var handlerCalled = false;
        RequestHandlerDelegate<Result<string>> next = _ =>
        {
            handlerCalled = true;
            return Task.FromResult(Result<string>.Success("handled"));
        };

        var result = await behavior.Handle(
            new TestRequest(string.Empty, 0),
            next,
            CancellationToken.None);

        Assert.False(handlerCalled);
        Assert.Equal(RequestErrors.ValidationFailed, result.Error);
        Assert.Equal("Validation.Failed", result.Error.Code);
        Assert.Equal("ข้อมูลไม่ถูกต้อง กรุณาตรวจสอบอีกครั้ง", result.Error.Message);
        Assert.Equal(2, result.ValidationFailures.Count);
        Assert.Contains(
            result.ValidationFailures,
            failure => failure.PropertyName == nameof(TestRequest.Name));
        Assert.Contains(
            result.ValidationFailures,
            failure => failure.PropertyName == nameof(TestRequest.Quantity));
    }

    [Fact]
    public async Task ValidRequestCallsHandlerAndReturnsResponse()
    {
        var validators = new IValidator<TestRequest>[]
        {
            new NameValidator(),
            new QuantityValidator(),
        };
        var behavior = new ValidationBehavior<TestRequest, Result<string>>(validators);
        var handlerCalled = false;
        RequestHandlerDelegate<Result<string>> next = _ =>
        {
            handlerCalled = true;
            return Task.FromResult(Result<string>.Success("handled"));
        };

        var response = await behavior.Handle(
            new TestRequest("Molly", 1),
            next,
            CancellationToken.None);

        Assert.True(handlerCalled);
        Assert.Equal("handled", response.Value);
    }

    [Fact]
    public async Task RequestWithoutValidatorsCallsHandler()
    {
        var behavior = new ValidationBehavior<TestRequest, Result<string>>([]);

        var response = await behavior.Handle(
            new TestRequest(string.Empty, 0),
            _ => Task.FromResult(Result<string>.Success("handled")),
            CancellationToken.None);

        Assert.Equal("handled", response.Value);
    }

    [Fact]
    public async Task ValidatorsRunSequentiallyAndAggregateInRegistrationOrder()
    {
        var probe = new ValidationProbe();
        var validators = new IValidator<TestRequest>[]
        {
            new TrackingValidator("first", probe),
            new TrackingValidator("second", probe),
        };
        var behavior = new ValidationBehavior<TestRequest, Result<string>>(validators);

        var result = await behavior.Handle(
            new TestRequest("Molly", 1),
            _ => Task.FromResult(Result<string>.Success("handled")),
            CancellationToken.None);

        Assert.Equal(1, probe.MaximumConcurrentValidators);
        Assert.Equal(
            ["first:start", "first:end", "second:start", "second:end"],
            probe.Events);
        Assert.Equal(
            ["first", "second"],
            result.ValidationFailures.Select(error => error.ErrorMessage));
    }

    private sealed record TestRequest(string Name, int Quantity)
        : IRequest<Result<string>>, IResultRequest<Result<string>>
    {
        public Result<string> CreateFailure(
            Error requestError,
            IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
            Result<string>.Failure(requestError, validationFailures);
    }

    private sealed class NameValidator : AbstractValidator<TestRequest>
    {
        public NameValidator()
        {
            RuleFor(request => request.Name).NotEmpty();
        }
    }

    private sealed class QuantityValidator : AbstractValidator<TestRequest>
    {
        public QuantityValidator()
        {
            RuleFor(request => request.Quantity).GreaterThan(0);
        }
    }

    private sealed class TrackingValidator : AbstractValidator<TestRequest>
    {
        public TrackingValidator(string name, ValidationProbe probe)
        {
            RuleFor(request => request.Name).CustomAsync(async (_, context, cancellationToken) =>
            {
                await probe.TrackAsync(name, cancellationToken);
                context.AddFailure(nameof(TestRequest.Name), name);
            });
        }
    }

    private sealed class ValidationProbe
    {
        private readonly List<string> events = [];
        private readonly object gate = new();
        private int activeValidators;

        public IReadOnlyList<string> Events
        {
            get
            {
                lock (gate)
                {
                    return events.ToArray();
                }
            }
        }

        public int MaximumConcurrentValidators { get; private set; }

        public async Task TrackAsync(string name, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                events.Add($"{name}:start");
                activeValidators++;
                MaximumConcurrentValidators = Math.Max(
                    MaximumConcurrentValidators,
                    activeValidators);
            }

            await Task.Delay(20, cancellationToken);

            lock (gate)
            {
                activeValidators--;
                events.Add($"{name}:end");
            }
        }
    }
}
