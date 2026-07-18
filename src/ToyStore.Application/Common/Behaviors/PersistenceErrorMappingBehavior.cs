using MediatR;
using ToyStore.Application.Common.Persistence;

namespace ToyStore.Application.Common.Behaviors;

public sealed class PersistenceErrorMappingBehavior<TRequest, TResponse>(
    IPersistenceFailureClassifier classifier)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        if (request is not IPersistenceFailureResultRequest<TResponse> persistenceRequest)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failure = classifier.Classify(exception);
            if (failure is null)
            {
                throw;
            }

            var error = persistenceRequest.MapPersistenceFailure(failure);
            if (error is null)
            {
                throw;
            }

            return persistenceRequest.CreateFailure(error);
        }
    }
}
