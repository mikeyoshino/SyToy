using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Inventory;
using ToyStore.Domain.Inventory;
using ToyStore.Infrastructure;

namespace ToyStore.UnitTests.Application.Inventory;

public sealed class InventoryMutationSessionContractTests
{
    [Fact]
    public void SessionExposesOnlyOperationScopedInventoryCapabilities()
    {
        var methods = typeof(IInventoryMutationSession).GetMethods();

        Assert.Contains(methods, method => method.Name == nameof(IInventoryMutationSession.ExecuteOnceAsync));
        Assert.Contains(methods, method => method.Name == nameof(IInventoryMutationSession.LockInventoryAsync));
        Assert.Contains(methods, method => method.Name == nameof(IInventoryMutationSession.FindMovementAsync));
        Assert.Contains(methods, method => method.Name == nameof(IInventoryMutationSession.FindReservationAsync));
        Assert.Equal(3, methods.Count(method => method.Name == nameof(IInventoryMutationSession.Add)));
        Assert.DoesNotContain(methods, method =>
            method.Name.Contains("Save", StringComparison.OrdinalIgnoreCase)
            || method.Name.Contains("Commit", StringComparison.OrdinalIgnoreCase)
            || method.Name.Contains("Rollback", StringComparison.OrdinalIgnoreCase)
            || method.Name.Contains("Reserve", StringComparison.OrdinalIgnoreCase)
            || method.Name.Contains("Batch", StringComparison.OrdinalIgnoreCase)
            || method.Name.Contains("Update", StringComparison.OrdinalIgnoreCase));

        var signatures = string.Join(
            '\n',
            methods.Select(method => method + string.Join(
                ',',
                method.GetParameters().Select(parameter => parameter.ParameterType.FullName))));
        Assert.DoesNotContain("EntityFrameworkCore", signatures, StringComparison.Ordinal);
        Assert.DoesNotContain("Npgsql", signatures, StringComparison.Ordinal);
        Assert.DoesNotContain("IQueryable", signatures, StringComparison.Ordinal);
        Assert.DoesNotContain("DbSet", signatures, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionAndVerificationContractsAreImmutableAndExplicit()
    {
        Assert.Equal(
            [
                InventoryCommitOutcome.Committed,
                InventoryCommitOutcome.DefinitelyRolledBack,
                InventoryCommitOutcome.Indeterminate,
            ],
            Enum.GetValues<InventoryCommitOutcome>());
        Assert.Equal(
            [
                InventoryCommitVerification.Committed,
                InventoryCommitVerification.Superseded,
                InventoryCommitVerification.Conflict,
                InventoryCommitVerification.Inconsistent,
                InventoryCommitVerification.Unavailable,
            ],
            Enum.GetValues<InventoryCommitVerification>());

        foreach (var type in new[]
                 {
                     typeof(InventoryMutationEvidence),
                     typeof(InventoryOperationIntent),
                     typeof(InventoryMutationExecution<>),
                     typeof(InventoryCommitVerificationResult),
                 })
        {
            Assert.All(type.GetProperties(), property =>
                Assert.False(property.SetMethod?.IsPublic ?? false));
        }
    }

    [Fact]
    public void SharedOperationIntentMatcherUsesAllProviderNeutralRetryEvidence()
    {
        var now = new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);
        var creation = InventoryItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2,
            "สินค้าเริ่มต้น", "initial", now, "admin");
        var operationId = Guid.NewGuid();
        var movement = creation.Item.ReceiveStock(
            operationId, 2, "รับสินค้า", "receive", 1,
            now.AddMinutes(1), "admin");
        InventoryOperationIntent Intent(
            string reason = "  รับสินค้า  ",
            string reference = "  receive  ",
            string actor = "  admin  ",
            long expectedSourceVersion = 1) =>
            InventoryOperationIntent.Create(
                operationId,
                creation.Item.Id,
                creation.Item.ProductId,
                StockMovementType.Received,
                2,
                expectedSourceVersion,
                reason,
                reference,
                actor);

        Assert.True(Intent().Matches(movement));
        Assert.False(Intent(reason: "เหตุผลอื่น").Matches(movement));
        Assert.False(Intent(reference: "other").Matches(movement));
        Assert.False(Intent(actor: "other-admin").Matches(movement));
        Assert.False(Intent(expectedSourceVersion: 2).Matches(movement));

        var signatures = string.Join(
            '\n',
            typeof(InventoryOperationIntent).Assembly
                .GetTypes()
                .Where(type => type == typeof(InventoryOperationIntent))
                .SelectMany(type => type.GetMembers())
                .Select(member => member.ToString()));
        Assert.DoesNotContain("EntityFrameworkCore", signatures, StringComparison.Ordinal);
        Assert.DoesNotContain("Npgsql", signatures, StringComparison.Ordinal);
    }

    [Fact]
    public void EvidenceCapturesExactMovementAndIntendedInventoryState()
    {
        var createdAt = new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);
        var creation = InventoryItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2,
            "สินค้าเริ่มต้น", "initial", createdAt, "admin");
        var item = creation.Item;
        var movement = item.ReceiveStock(
            Guid.NewGuid(), 3, "รับสินค้า", "receive", item.Version,
            createdAt.AddMinutes(1), "admin-2");

        var evidence = InventoryMutationEvidence.Capture(item, movement);

        Assert.Equal(movement.Id, evidence.OperationId);
        Assert.Equal(item.Id, evidence.InventoryItemId);
        Assert.Equal(item.ProductId, evidence.ProductId);
        Assert.Equal(item.OnHandQuantity, evidence.IntendedOnHandQuantity);
        Assert.Equal(item.HeldQuantity, evidence.IntendedHeldQuantity);
        Assert.Equal(item.Version, evidence.IntendedVersion);
        Assert.Equal(item.UpdatedAtUtc, evidence.IntendedUpdatedAtUtc);
        Assert.Equal(item.UpdatedBy, evidence.IntendedUpdatedBy);
        Assert.Equal(movement.Type, evidence.MovementType);
        Assert.Equal(movement.QuantityDelta, evidence.QuantityDelta);
        Assert.Equal(movement.ResultingOnHandQuantity, evidence.ResultingOnHandQuantity);
        Assert.Equal(movement.ResultingInventoryVersion, evidence.ResultingInventoryVersion);
        Assert.Equal(movement.Reason, evidence.Reason);
        Assert.Equal(movement.Reference, evidence.Reference);
        Assert.Equal(movement.Actor, evidence.Actor);
        Assert.Equal(movement.OccurredAtUtc, evidence.OccurredAtUtc);
        Assert.Equal(movement.ReservationId, evidence.ReservationId);
    }

    [Fact]
    public void InfrastructureRegistersSingletonInventorySessionFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] =
                    "Host=localhost;Database=toystore_tests;Username=test;Password=test",
            })
            .Build());

        var descriptor = Assert.Single(
            services,
            service => service.ServiceType == typeof(IInventoryMutationSessionFactory));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void ApplicationRegistersTransientInventoryCommitOutcomeResolver()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();

        var descriptor = Assert.Single(
            services,
            service => service.ServiceType == typeof(InventoryCommitOutcomeResolver));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public async Task SessionContractCanRepresentTypedRollbackWithoutProviderTypes()
    {
        var execution = new InventoryMutationExecution<string>(
            Result<string>.Failure(new Error("Test.Failure", "ล้มเหลว", ErrorType.Conflict)),
            InventoryCommitOutcome.DefinitelyRolledBack);

        Assert.True(execution.Result.IsFailure);
        Assert.Equal(InventoryCommitOutcome.DefinitelyRolledBack, execution.CommitOutcome);
        Assert.Null(execution.CommitFailure);
        await Task.CompletedTask;
    }
}
