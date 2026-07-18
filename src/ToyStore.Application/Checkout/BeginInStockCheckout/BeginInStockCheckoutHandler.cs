using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Addresses;
using ToyStore.Domain.Checkouts;

namespace ToyStore.Application.Checkout.BeginInStockCheckout;

public sealed class BeginInStockCheckoutHandler(
    IInStockCheckoutStore checkoutStore,
    IPaymentGateway paymentGateway,
    ICheckoutCustomerReader customerReader,
    IThaiAddressCatalog addressCatalog,
    TimeProvider timeProvider)
    : IRequestHandler<BeginInStockCheckoutCommand, Result<BeginInStockCheckoutResult>>
{
    public async Task<Result<BeginInStockCheckoutResult>> Handle(
        BeginInStockCheckoutCommand request,
        CancellationToken cancellationToken)
    {
        var customerId = request.AuthorizedActorId
            ?? throw new InvalidOperationException("In-stock checkout requires an authorized customer.");
        if (!addressCatalog.IsValid(request.Province, request.District, request.SubDistrict, request.PostalCode))
            return Result<BeginInStockCheckoutResult>.Failure(CheckoutErrors.AddressInvalid);
        var customerEmail = await customerReader.GetEmailAsync(customerId, cancellationToken);
        if (string.IsNullOrWhiteSpace(customerEmail))
            return Result<BeginInStockCheckoutResult>.Failure(CheckoutErrors.CustomerEmailMissing);

        ShippingAddressSnapshot address;
        try
        {
            address = ShippingAddressSnapshot.Create(request.RecipientName, request.PhoneNumber,
                request.AddressLine, request.SubDistrict, request.District, request.Province, request.PostalCode);
        }
        catch (ArgumentException)
        {
            return Result<BeginInStockCheckoutResult>.Failure(CheckoutErrors.AddressInvalid);
        }

        var prepared = await checkoutStore.PrepareAsync(new(Guid.NewGuid(), customerId, address,
            request.ClientRequestId.ToString("N"), timeProvider.GetUtcNow().ToUniversalTime()), cancellationToken);
        if (prepared.IsFailure)
            return Result<BeginInStockCheckoutResult>.Failure(prepared.Error, prepared.ValidationFailures);

        PaymentSessionResult payment;
        try
        {
            payment = await paymentGateway.CreateInStockSessionAsync(new(
                prepared.Value.CheckoutAttemptId,
                prepared.Value.IdempotencyKey,
                prepared.Value.CustomerId,
                customerEmail,
                prepared.Value.Items.Select(item => new InStockPaymentLine(
                    item.ProductName, item.Quantity, item.UnitPrice)).ToArray(),
                prepared.Value.ExpiresAtUtc), cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<BeginInStockCheckoutResult>.Failure(CheckoutErrors.PaymentUnavailable);
        }

        var attached = await checkoutStore.AttachProviderSessionAsync(customerId,
            prepared.Value.CheckoutAttemptId, payment.SessionId, cancellationToken);
        if (attached.IsFailure)
            return Result<BeginInStockCheckoutResult>.Failure(attached.Error, attached.ValidationFailures);

        var checkout = attached.Value;
        return Result<BeginInStockCheckoutResult>.Success(new(checkout.CheckoutAttemptId,
            payment.ClientSecret, paymentGateway.PublishableKey, checkout.Items,
            checkout.ShippingAmount, checkout.PaymentAmount, checkout.ExpiresAtUtc));
    }
}
