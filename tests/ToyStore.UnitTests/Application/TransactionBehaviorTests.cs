using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Interfaces;
using ToyStore.Application.Common.Messaging;

namespace ToyStore.UnitTests.Application;

public sealed class TransactionBehaviorTests
{
    [Fact]
    public async Task HandleDelegatesEntireHandlerExecutionAndReturnsResponse()
    {
        var context = new FakeApplicationDbContext();
        var behavior = new TransactionBehavior<TestCommand, string>(context);
        var handlerCalled = false;
        RequestHandlerDelegate<string> next = _ =>
        {
            Assert.True(context.IsExecutingTransaction);
            handlerCalled = true;
            return Task.FromResult("created");
        };

        var response = await behavior.Handle(
            new TestCommand(),
            next,
            CancellationToken.None);

        Assert.True(handlerCalled);
        Assert.Equal("created", response);
        Assert.Equal(1, context.TransactionCount);
        Assert.False(context.IsExecutingTransaction);
    }

    [Fact]
    public async Task HandleForwardsCancellationTokenToTransactionBoundary()
    {
        var context = new FakeApplicationDbContext();
        var behavior = new TransactionBehavior<TestCommand, string>(context);
        using var source = new CancellationTokenSource();
        var handlerCancellationToken = CancellationToken.None;

        await behavior.Handle(
            new TestCommand(),
            cancellationToken =>
            {
                handlerCancellationToken = cancellationToken;
                return Task.FromResult("created");
            },
            source.Token);

        Assert.Equal(source.Token, context.ReceivedCancellationToken);
        Assert.Equal(source.Token, handlerCancellationToken);
    }

    [Fact]
    public void TransactionBehaviorRequiresACommandRequest()
    {
        var requestConstraint = typeof(TransactionBehavior<,>)
            .GetGenericArguments()[0]
            .GetGenericParameterConstraints();

        Assert.Contains(
            requestConstraint,
            constraint => constraint.IsGenericType
                && constraint.GetGenericTypeDefinition() == typeof(ICommand<>));
    }

    [Fact]
    public void AddApplicationRegistersBehaviorsInRequiredOrder()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        var behaviorTypes = services
            .Where(descriptor => descriptor.ServiceType == typeof(IPipelineBehavior<,>))
            .Select(descriptor => descriptor.ImplementationType)
            .ToArray();

        Assert.Equal(
            [
                typeof(LoggingBehavior<,>),
                typeof(AuthorizationBehavior<,>),
                typeof(ValidationBehavior<,>),
                typeof(PersistenceErrorMappingBehavior<,>),
                typeof(TransactionBehavior<,>),
            ],
            behaviorTypes);
    }

    private sealed record TestCommand : ICommand<string>;

    private sealed class FakeApplicationDbContext : IApplicationDbContext
    {
        public bool IsExecutingTransaction { get; private set; }

        public int TransactionCount { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(0);

        public async Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken)
        {
            TransactionCount++;
            ReceivedCancellationToken = cancellationToken;
            IsExecutingTransaction = true;

            try
            {
                return await operation(cancellationToken);
            }
            finally
            {
                IsExecutingTransaction = false;
            }
        }
    }
}
