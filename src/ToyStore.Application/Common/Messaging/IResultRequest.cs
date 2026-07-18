using MediatR;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Common.Messaging;

public interface IResultRequest<out TResponse> : IRequest<TResponse>
{
    TResponse CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null);
}
