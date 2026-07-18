using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Models;
using ToyStore.Application.PreOrders;
using ToyStore.Application.PreOrders.GetPreOrderCheckoutEligibility;
using ToyStore.Domain.Products;

namespace ToyStore.UnitTests.Application.PreOrders;

public sealed class PreOrderCheckoutEligibilityTests
{
    [Fact]
    public async Task QueryIsCustomerOnlyActorFreeAndFluentValidationIsThai()
    {
        var query = new GetPreOrderCheckoutEligibilityQuery(Guid.Empty, 0);

        Assert.Equal(PolicyNames.CanUseCustomerCart, query.RequiredPolicy);
        Assert.Equal(["ProductId", "Quantity"], query.GetType().GetConstructors().Single()
            .GetParameters().Select(x => x.Name!).ToArray());
        var validation = await new GetPreOrderCheckoutEligibilityValidator().ValidateAsync(
            query, TestContext.Current.CancellationToken);
        Assert.Contains(validation.Errors, x => x.PropertyName == nameof(query.ProductId)
            && x.ErrorMessage == "รหัสสินค้าไม่ถูกต้อง");
        Assert.Contains(validation.Errors, x => x.PropertyName == nameof(query.Quantity)
            && x.ErrorMessage == "จำนวนต้องมากกว่า 0");
    }

    [Theory]
    [InlineData(false, false, "Authorization.Unauthorized")]
    [InlineData(true, false, "Authorization.Forbidden")]
    public async Task AuthorizationStopsReaderBeforeHandler(
        bool authenticated,
        bool authorized,
        string code)
    {
        var reader = new FakeReader(Eligible());
        var handler = new GetPreOrderCheckoutEligibilityHandler(reader, new FixedTimeProvider(OpenAt));
        var query = new GetPreOrderCheckoutEligibilityQuery(ProductId, 1);
        var behavior = new AuthorizationBehavior<GetPreOrderCheckoutEligibilityQuery,
            Result<PreOrderCheckoutEligibilityResult>>(new Authorization(authenticated, authorized));

        var result = await behavior.Handle(query, token => handler.Handle(query, token),
            TestContext.Current.CancellationToken);

        Assert.Equal(code, result.Error.Code);
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task EligibilityReturnsAuthoritativeSnapshotAndRetryNeverWrites()
    {
        var reader = new FakeReader(Eligible());
        var handler = new GetPreOrderCheckoutEligibilityHandler(reader, new FixedTimeProvider(OpenAt));
        var query = new GetPreOrderCheckoutEligibilityQuery(ProductId, 2);

        var first = await Authorize(query, handler);
        var retry = await Authorize(query, handler);

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Value, retry.Value);
        Assert.Equal(2, reader.CallCount);
        Assert.Equal("customer-1", reader.LastCustomerId);
        Assert.Equal(3, first.Value.RemainingCapacity);
        Assert.Equal(1, first.Value.CustomerAllocatedQuantity);
        Assert.Equal(2, first.Value.CustomerRemainingAllowance);
        Assert.Equal(800m, first.Value.BalanceAmount);
        Assert.Equal(PreOrderDepositPolicy.NonRefundableOnCustomerCancellationOrBalanceOverdue,
            first.Value.DepositPolicy);
    }

    [Theory]
    [InlineData("missing", "PreOrderCapacity.NotAvailable")]
    [InlineData("draft", "PreOrderCapacity.NotAvailable")]
    [InlineData("instock", "PreOrderCapacity.NotAvailable")]
    [InlineData("closed", "PreOrderCapacity.Closed")]
    [InlineData("empty", "PreOrderCapacity.InsufficientCapacity")]
    [InlineData("limit", "PreOrderCapacity.CustomerLimitExceeded")]
    public async Task HandlerReturnsTypedAvailabilityFailures(string scenario, string code)
    {
        var model = scenario switch
        {
            "missing" => null,
            "draft" => Eligible() with { Status = ProductStatus.Draft },
            "instock" => Eligible() with { SaleType = SaleType.InStock },
            "empty" => Eligible() with { RemainingCapacity = 0 },
            "limit" => Eligible() with { CustomerAllocatedQuantity = 3 },
            _ => Eligible(),
        };
        var now = scenario == "closed" ? CloseAt : OpenAt;
        var handler = new GetPreOrderCheckoutEligibilityHandler(
            new FakeReader(model), new FixedTimeProvider(now));
        var query = new GetPreOrderCheckoutEligibilityQuery(ProductId, 1);

        var result = await Authorize(query, handler);

        Assert.Equal(code, result.Error.Code);
    }

    [Fact]
    public async Task ExactCloseIsClosedAndLimitArithmeticCannotOverflow()
    {
        var exact = new GetPreOrderCheckoutEligibilityHandler(
            new FakeReader(Eligible()), new FixedTimeProvider(CloseAt));
        var overflow = new GetPreOrderCheckoutEligibilityHandler(
            new FakeReader(Eligible() with
            {
                MaxPerCustomer = int.MaxValue,
                CustomerAllocatedQuantity = int.MaxValue,
                TotalCapacity = int.MaxValue,
                RemainingCapacity = int.MaxValue,
            }), new FixedTimeProvider(OpenAt));

        var closed = await Authorize(
            new GetPreOrderCheckoutEligibilityQuery(ProductId, 1), exact);
        var limited = await Authorize(
            new GetPreOrderCheckoutEligibilityQuery(ProductId, int.MaxValue), overflow);

        Assert.Equal(PreOrderCapacityErrors.Closed, closed.Error);
        Assert.Equal(PreOrderCapacityErrors.CustomerLimitExceeded, limited.Error);
    }

    private static Task<Result<PreOrderCheckoutEligibilityResult>> Authorize(
        GetPreOrderCheckoutEligibilityQuery query,
        GetPreOrderCheckoutEligibilityHandler handler) =>
        new AuthorizationBehavior<GetPreOrderCheckoutEligibilityQuery,
            Result<PreOrderCheckoutEligibilityResult>>(new Authorization(true, true)).Handle(
                query,
                token => handler.Handle(query, token),
                TestContext.Current.CancellationToken);

    private static PreOrderCheckoutEligibilityReadModel Eligible() => new(
        ProductId, "สินค้า", "Product", "product", ProductStatus.Published, SaleType.PreOrder,
        1000, 200, CloseAt, 12, 2026, 7,
        CapacityId, 5, 3, 4, 3, 1);

    private static readonly Guid ProductId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid CapacityId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset CloseAt = new(2026, 8, 1, 16, 59, 59, TimeSpan.Zero);
    private static readonly DateTimeOffset OpenAt = CloseAt.AddTicks(-1);

    private sealed class FakeReader(PreOrderCheckoutEligibilityReadModel? model)
        : IPreOrderCheckoutEligibilityReader
    {
        public int CallCount { get; private set; }
        public string? LastCustomerId { get; private set; }

        public Task<PreOrderCheckoutEligibilityReadModel?> ReadAsync(
            Guid productId,
            string customerId,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastCustomerId = customerId;
            return Task.FromResult(model);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Authorization(bool authenticated, bool authorized)
        : ICurrentUserAuthorization
    {
        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policy,
            CancellationToken cancellationToken) => Task.FromResult(new CurrentUserAuthorizationResult(
                authenticated, authorized, authorized ? "customer-1" : null));
    }
}
