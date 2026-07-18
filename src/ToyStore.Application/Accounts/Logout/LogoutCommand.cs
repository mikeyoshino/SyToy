using MediatR;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Accounts.Logout;

public sealed record LogoutCommand : IRequest<Result>;
