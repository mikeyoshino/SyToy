using MediatR;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Accounts.Login;

public sealed record LoginCommand(
    string Email,
    string Password,
    bool RememberMe) : IRequest<Result<LoginResult>>;
