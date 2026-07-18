using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Orders;
using ToyStore.Application.Orders.GetCustomerOrder;
using ToyStore.Application.Orders.ListCustomerOrders;

namespace ToyStore.UnitTests.Application.Orders;

public sealed class CustomerOrderQueryContractTests
{
    [Fact]
    public void QueriesRequireCustomerOrderPolicyAndNeverAcceptCustomerIdentity()
    {
        var list = new ListCustomerOrdersQuery();
        var detail = new GetCustomerOrderQuery("SY-20260718-0001");

        Assert.Equal(PolicyNames.CanViewCustomerOrders, list.RequiredPolicy);
        Assert.Equal(PolicyNames.CanViewCustomerOrders, detail.RequiredPolicy);
        Assert.DoesNotContain(typeof(ListCustomerOrdersQuery).GetProperties(),
            property => property.Name.Contains("Customer", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(GetCustomerOrderQuery).GetProperties(),
            property => property.Name.Contains("Customer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidatorsEnforcePaginationAndOrderNumberWithThaiMessages()
    {
        var list = new ListCustomerOrdersValidator().Validate(
            new ListCustomerOrdersQuery(0, 51, new string('ก', 101)));
        var detail = new GetCustomerOrderValidator().Validate(
            new GetCustomerOrderQuery(string.Empty));

        Assert.False(list.IsValid);
        Assert.False(detail.IsValid);
        Assert.All(list.Errors.Concat(detail.Errors), failure =>
            Assert.Contains(failure.ErrorMessage,
                character => character is >= '\u0E00' and <= '\u0E7F'));
    }

    [Fact]
    public async Task DetailUsesAuthorizedActorForOwnershipAndHidesCrossCustomerOrder()
    {
        var reader = new RecordingReader();
        var handler = new GetCustomerOrderHandler(reader);
        var query = new GetCustomerOrderQuery("ORDER-OTHER");
        var behavior = new AuthorizationBehavior<GetCustomerOrderQuery,
            Result<CustomerOrderDetailView>>(new CustomerAuthorization());

        var result = await behavior.Handle(
            query,
            token => handler.Handle(query, token),
            TestContext.Current.CancellationToken);

        Assert.Equal("customer-current", reader.CustomerId);
        Assert.Equal(CustomerOrderErrors.NotFound, result.Error);
    }

    private sealed class RecordingReader : ICustomerOrderReader
    {
        public string? CustomerId { get; private set; }

        public Task<CustomerOrderPage> ListAsync(
            string customerId,
            int page,
            int pageSize,
            string? searchTerm,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CustomerOrderPage([], page, pageSize, 0));

        public Task<CustomerOrderDetailView?> GetAsync(
            string customerId,
            string orderNumber,
            CancellationToken cancellationToken)
        {
            CustomerId = customerId;
            return Task.FromResult<CustomerOrderDetailView?>(null);
        }
    }

    private sealed class CustomerAuthorization : ICurrentUserAuthorization
    {
        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policyName,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CurrentUserAuthorizationResult(
                true,
                true,
                "customer-current"));
    }
}
