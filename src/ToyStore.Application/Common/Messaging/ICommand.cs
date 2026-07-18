using MediatR;

namespace ToyStore.Application.Common.Messaging;

public interface ICommand<out TResponse> : IRequest<TResponse>;
