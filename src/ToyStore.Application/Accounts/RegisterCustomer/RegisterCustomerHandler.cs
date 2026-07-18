using MediatR;
using ToyStore.Application.Common.Interfaces;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Accounts.RegisterCustomer;

public sealed class RegisterCustomerHandler(IIdentityService identityService)
    : IRequestHandler<RegisterCustomerCommand, Result<string>>
{
    public Task<Result<string>> Handle(
        RegisterCustomerCommand request,
        CancellationToken cancellationToken) =>
        identityService.RegisterCustomerAsync(
            request.Email,
            request.Password,
            cancellationToken);
}
