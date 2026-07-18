using FluentValidation;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Inventory;
using ToyStore.Application.Inventory.GetInventoryAvailability;
using ToyStore.Application.Inventory.ListStockMovements;
using ToyStore.Domain.Inventory;

namespace ToyStore.UnitTests.Application.Inventory;

public sealed class InventoryReadQueryTests
{
    [Fact]
    public async Task QueriesUseAdminPolicyThaiValidationAndCanonicalPagingDefaults()
    {
        var availability = new GetInventoryAvailabilityQuery(Guid.Empty, Guid.Empty);
        var movements = new ListStockMovementsQuery(Guid.Empty, Guid.Empty);

        Assert.Equal(PolicyNames.CanManageProducts, availability.RequiredPolicy);
        Assert.Equal(PolicyNames.CanManageProducts, movements.RequiredPolicy);
        Assert.Equal(1, movements.Page);
        Assert.Equal(20, movements.PageSize);
        var availabilityFailures = await new GetInventoryAvailabilityValidator()
            .ValidateAsync(availability, TestContext.Current.CancellationToken);
        var movementFailures = await new ListStockMovementsValidator()
            .ValidateAsync(
                movements with { Page = 0, PageSize = 101 },
                TestContext.Current.CancellationToken);
        Assert.All(
            availabilityFailures.Errors.Concat(movementFailures.Errors),
            failure => Assert.Contains(
                failure.ErrorMessage,
                character => character is >= '\u0E00' and <= '\u0E7F'));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task AvailabilityAuthorizationStopsBeforeValidationReaderAndClock(
        bool authenticated,
        bool authorized)
    {
        var store = new FakeReadStore();
        var clock = new CountingTimeProvider();
        var query = new GetInventoryAvailabilityQuery(Guid.Empty, Guid.Empty);
        var validator = new CountingAvailabilityValidator();
        var authorization = new AuthorizationBehavior<
            GetInventoryAvailabilityQuery,
            Result<InventoryAvailabilityResult>>(
                new StubAuthorization(authenticated, authorized));
        var validation = new ValidationBehavior<
            GetInventoryAvailabilityQuery,
            Result<InventoryAvailabilityResult>>([validator]);
        var handler = new GetInventoryAvailabilityHandler(store, clock);

        var result = await authorization.Handle(
            query,
            token => validation.Handle(
                query,
                nextToken => handler.Handle(query, nextToken),
                token),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            authenticated ? ErrorType.Forbidden : ErrorType.Unauthorized,
            result.Error.Type);
        Assert.Equal(0, validator.CallCount);
        Assert.Equal(0, store.AvailabilityCalls);
        Assert.Equal(0, clock.CallCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task MovementHistoryAuthorizationStopsBeforeValidationAndReader(
        bool authenticated,
        bool authorized)
    {
        var store = new FakeReadStore();
        var query = new ListStockMovementsQuery(
            Guid.Empty,
            Guid.Empty,
            Page: 0,
            PageSize: 101);
        var validator = new CountingMovementValidator();
        var authorization = new AuthorizationBehavior<
            ListStockMovementsQuery,
            Result<PagedResult<StockMovementListItem>>>(
                new StubAuthorization(authenticated, authorized));
        var validation = new ValidationBehavior<
            ListStockMovementsQuery,
            Result<PagedResult<StockMovementListItem>>>([validator]);
        var handler = new ListStockMovementsHandler(store);

        var result = await authorization.Handle(
            query,
            token => validation.Handle(
                query,
                nextToken => handler.Handle(query, nextToken),
                token),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            authenticated ? ErrorType.Forbidden : ErrorType.Unauthorized,
            result.Error.Type);
        Assert.Equal(0, validator.CallCount);
        Assert.Equal(0, store.MovementCalls);
    }

    [Fact]
    public async Task AvailabilityHandlerPreservesUtcCancellationAndFailsClosedOnMismatch()
    {
        var store = new FakeReadStore
        {
            Availability = new InventoryAvailabilityReadModel(
                Guid.NewGuid(), Guid.NewGuid(), 10, 4, 3, 1, 2,
                UtcNow, "admin"),
        };
        var clock = new CountingTimeProvider();
        var handler = new GetInventoryAvailabilityHandler(store, clock);
        var query = new GetInventoryAvailabilityQuery(
            store.Availability.InventoryItemId,
            store.Availability.ProductId);
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(query, cancellation.Token));

        Assert.Equal(UtcNow, store.ReceivedNowUtc);
        Assert.Equal(cancellation.Token, store.ReceivedCancellationToken);
    }

    [Fact]
    public async Task AvailabilityAndHistoryHandlersMapSafeReadModels()
    {
        var inventoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var movementId = Guid.NewGuid();
        var store = new FakeReadStore
        {
            Availability = new InventoryAvailabilityReadModel(
                inventoryId, productId, 10, 4, 4, 1, 3,
                UtcNow, "admin"),
            MovementPage = new StockMovementReadPage(
                [new StockMovementReadModel(
                    movementId, inventoryId, productId, StockMovementType.Received,
                    2, 10, 3, "รับสินค้า", "ref", "admin", null, UtcNow)],
                EffectivePageNumber: 2,
                TotalCount: 3),
        };

        var availability = await new GetInventoryAvailabilityHandler(
            store,
            new CountingTimeProvider()).Handle(
                new GetInventoryAvailabilityQuery(inventoryId, productId),
                TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        var history = await new ListStockMovementsHandler(store).Handle(
            new ListStockMovementsQuery(inventoryId, productId, Page: 99, PageSize: 2),
            cancellation.Token);

        Assert.Equal(4, availability.Value.PhysicalHeldQuantity);
        Assert.Equal(6, availability.Value.ReservableQuantity);
        Assert.Equal(1, availability.Value.EffectiveReservedQuantity);
        Assert.Equal(9, availability.Value.CustomerAvailableQuantity);
        Assert.Equal(2, history.Value.PageNumber);
        Assert.Equal(movementId, Assert.Single(history.Value.Items).Id);
        Assert.Equal(1, store.MovementCalls);
        Assert.Equal(cancellation.Token, store.ReceivedMovementCancellationToken);
    }

    private static readonly DateTimeOffset UtcNow =
        new(2026, 7, 17, 4, 0, 0, TimeSpan.Zero);

    private sealed class FakeReadStore : IInventoryReadStore
    {
        public InventoryAvailabilityReadModel? Availability { get; set; }

        public StockMovementReadPage? MovementPage { get; set; }

        public int AvailabilityCalls { get; private set; }

        public int MovementCalls { get; private set; }

        public DateTimeOffset ReceivedNowUtc { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public CancellationToken ReceivedMovementCancellationToken { get; private set; }

        public Task<InventoryAvailabilityReadModel?> ReadAvailabilityAsync(
            Guid inventoryItemId,
            Guid productId,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            AvailabilityCalls++;
            ReceivedNowUtc = nowUtc;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(Availability);
        }

        public Task<StockMovementReadPage?> ReadMovementsAsync(
            StockMovementReadRequest request,
            CancellationToken cancellationToken)
        {
            MovementCalls++;
            ReceivedMovementCancellationToken = cancellationToken;
            return Task.FromResult(MovementPage);
        }
    }

    private sealed class CountingTimeProvider : TimeProvider
    {
        public int CallCount { get; private set; }

        public override DateTimeOffset GetUtcNow()
        {
            CallCount++;
            return UtcNow;
        }
    }

    private sealed class StubAuthorization(bool authenticated, bool authorized)
        : ICurrentUserAuthorization
    {
        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policyName,
            CancellationToken cancellationToken) => Task.FromResult(
                new CurrentUserAuthorizationResult(
                    authenticated,
                    authorized,
                    authenticated ? "admin" : null));
    }

    private sealed class CountingAvailabilityValidator
        : AbstractValidator<GetInventoryAvailabilityQuery>
    {
        public CountingAvailabilityValidator()
        {
            RuleFor(query => query.InventoryItemId).Custom((_, context) =>
            {
                CallCount++;
                context.AddFailure("invalid");
            });
        }

        public int CallCount { get; private set; }
    }

    private sealed class CountingMovementValidator
        : AbstractValidator<ListStockMovementsQuery>
    {
        public CountingMovementValidator()
        {
            RuleFor(query => query.InventoryItemId).Custom((_, context) =>
            {
                CallCount++;
                context.AddFailure("invalid");
            });
        }

        public int CallCount { get; private set; }
    }
}
