namespace ToyStore.Application.Checkout;

public interface ICheckoutCustomerReader
{
    Task<string?> GetEmailAsync(string customerId, CancellationToken cancellationToken);
}
