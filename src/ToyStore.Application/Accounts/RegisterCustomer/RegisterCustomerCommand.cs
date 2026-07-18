using MediatR;
using ToyStore.Application.Common.Messaging;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Accounts.RegisterCustomer;

public sealed record RegisterCustomerCommand(
    string Email,
    string Password,
    string ConfirmPassword) : IRequest<Result<string>>, IResultRequest<Result<string>>
{
    public Result<string> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<string>.Failure(requestError, validationFailures);
}
