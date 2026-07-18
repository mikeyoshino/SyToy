using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ToyStore.Application.Accounts;
using ToyStore.Application.Accounts.BootstrapAdmin;
using ToyStore.Application.Common.Interfaces;
using ToyStore.Application.Common.Models;
using ToyStore.Web.Identity;

namespace ToyStore.UnitTests.Web;

public sealed class AdminBootstrapCommandTests
{
    [Theory]
    [InlineData("--bootstrap-admin", true)]
    [InlineData("--bootstrap-admin=true", false)]
    [InlineData("bootstrap-admin", false)]
    [InlineData("--BOOTSTRAP-ADMIN", false)]
    public void DetectsOnlyTheExactBootstrapFlag(string argument, bool expected)
    {
        Assert.Equal(expected, AdminBootstrapCommand.IsRequested([argument]));
    }

    [Fact]
    public async Task MissingConfigurationFailsWithoutCallingBootstrapper()
    {
        var bootstrapper = new FakeAdminBootstrapper();
        var logger = new CapturingLogger<AdminBootstrapCommand>();
        var command = new AdminBootstrapCommand(
            new ConfigurationBuilder().Build(),
            bootstrapper,
            logger);

        var exitCode = await command.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.False(bootstrapper.Called);
        Assert.DoesNotContain(logger.Messages, message => message.Contains('@', StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConfiguredCommandPassesSecretWithoutLoggingIt()
    {
        const string password = "SENTINEL-Temporary1";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BootstrapAdmin:Email"] = "admin@example.com",
                ["BootstrapAdmin:TemporaryPassword"] = password,
            })
            .Build();
        var bootstrapper = new FakeAdminBootstrapper
        {
            Result = Result<BootstrapAdminResult>.Success(new BootstrapAdminResult("admin-1")),
        };
        var logger = new CapturingLogger<AdminBootstrapCommand>();
        var command = new AdminBootstrapCommand(configuration, bootstrapper, logger);

        var exitCode = await command.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal("admin@example.com", bootstrapper.Email);
        Assert.Equal(password, bootstrapper.Password);
        Assert.DoesNotContain(
            logger.Messages,
            message => message.Contains(password, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExpectedBootstrapFailureReturnsNonZeroWithoutThrowing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BootstrapAdmin:Email"] = "admin@example.com",
                ["BootstrapAdmin:TemporaryPassword"] = "Temporary1",
            })
            .Build();
        var bootstrapper = new FakeAdminBootstrapper
        {
            Result = Result<BootstrapAdminResult>.Failure(AccountErrors.AdminAlreadyExists),
        };
        var command = new AdminBootstrapCommand(
            configuration,
            bootstrapper,
            new CapturingLogger<AdminBootstrapCommand>());

        var exitCode = await command.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
    }

    private sealed class FakeAdminBootstrapper : IAdminBootstrapper
    {
        public Result<BootstrapAdminResult> Result { get; set; } =
            Result<BootstrapAdminResult>.Failure(AccountErrors.AdminBootstrapFailed);

        public bool Called { get; private set; }

        public string? Email { get; private set; }

        public string? Password { get; private set; }

        public Task<Result<BootstrapAdminResult>> CreateFirstAdminAsync(
            string email,
            string temporaryPassword,
            CancellationToken cancellationToken)
        {
            Called = true;
            Email = email;
            Password = temporaryPassword;
            return Task.FromResult(Result);
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
