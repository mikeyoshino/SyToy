using MediatR;
using ToyStore.Application.Common.Interfaces;
using ToyStore.Application.Common.Messaging;

namespace ToyStore.Application.Common.Behaviors;

public sealed class TransactionBehavior<TRequest, TResponse>(IApplicationDbContext dbContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        return dbContext.ExecuteInTransactionAsync(
            transactionCancellationToken => next(transactionCancellationToken),
            cancellationToken);
    }
}
