using FluentValidation;
using MediatR;
using ToyStore.Application.Common.Messaging;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IValidator<TRequest>[] validators =
        validators?.ToArray() ?? throw new ArgumentNullException(nameof(validators));

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        if (validators.Length == 0)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var failures = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var validator in validators)
        {
            var validationResult = await validator.ValidateAsync(
                new ValidationContext<TRequest>(request),
                cancellationToken).ConfigureAwait(false);
            failures.AddRange(validationResult.Errors);
        }

        if (failures.Count != 0)
        {
            if (request is not IResultRequest<TResponse> resultRequest)
            {
                throw new InvalidOperationException(
                    $"Validated request {typeof(TRequest).Name} must implement IResultRequest.");
            }

            var fieldFailures = failures
                .Select(failure => new FieldValidationFailure(
                    failure.PropertyName,
                    failure.ErrorMessage))
                .ToArray();
            return resultRequest.CreateFailure(RequestErrors.ValidationFailed, fieldFailures);
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }
}
