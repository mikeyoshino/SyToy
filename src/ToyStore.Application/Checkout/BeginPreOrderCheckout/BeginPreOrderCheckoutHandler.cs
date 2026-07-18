using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Addresses;
using ToyStore.Domain.Checkouts;

namespace ToyStore.Application.Checkout.BeginPreOrderCheckout;

public sealed class BeginPreOrderCheckoutHandler(
    IPreOrderCheckoutStore repository,
    IPaymentGateway paymentGateway,
    ICheckoutCustomerReader customerReader,
    IThaiAddressCatalog addressCatalog,
    TimeProvider timeProvider)
    : IRequestHandler<BeginPreOrderCheckoutCommand, Result<BeginPreOrderCheckoutResult>>
{
    public async Task<Result<BeginPreOrderCheckoutResult>> Handle(
        BeginPreOrderCheckoutCommand request,
        CancellationToken cancellationToken)
    {
        var customerId = request.AuthorizedActorId
            ?? throw new InvalidOperationException("Direct Pre-order checkout requires an authorized customer.");
        if (!addressCatalog.IsValid(request.Province, request.District, request.SubDistrict, request.PostalCode))
            return Result<BeginPreOrderCheckoutResult>.Failure(CheckoutErrors.AddressInvalid);
        var customerEmail = await customerReader.GetEmailAsync(customerId, cancellationToken);
        if (string.IsNullOrWhiteSpace(customerEmail))
            return Result<BeginPreOrderCheckoutResult>.Failure(CheckoutErrors.CustomerEmailMissing);

        ShippingAddressSnapshot address;
        try
        {
            address = ShippingAddressSnapshot.Create(request.RecipientName, request.PhoneNumber,
                request.AddressLine, request.SubDistrict, request.District, request.Province, request.PostalCode);
        }
        catch (ArgumentException)
        {
            return Result<BeginPreOrderCheckoutResult>.Failure(CheckoutErrors.AddressInvalid);
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var prepared = await repository.PrepareAsync(new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), request.ProductId, request.Quantity,
            customerId, address, request.ClientRequestId.ToString("N"), now), cancellationToken);
        if (prepared.IsFailure)
            return Result<BeginPreOrderCheckoutResult>.Failure(prepared.Error, prepared.ValidationFailures);

        PaymentSessionResult payment;
        try
        {
            payment = await paymentGateway.CreatePreOrderDepositSessionAsync(new(
                prepared.Value.CheckoutAttemptId,
                prepared.Value.IdempotencyKey,
                prepared.Value.CustomerId,
                customerEmail,
                prepared.Value.ProductName,
                prepared.Value.Quantity,
                prepared.Value.DepositAmount,
                prepared.Value.ExpiresAtUtc), cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<BeginPreOrderCheckoutResult>.Failure(CheckoutErrors.PaymentUnavailable);
        }

        var attached = await repository.AttachProviderSessionAsync(customerId,
            prepared.Value.CheckoutAttemptId, payment.SessionId, cancellationToken);
        if (attached.IsFailure)
            return Result<BeginPreOrderCheckoutResult>.Failure(attached.Error, attached.ValidationFailures);

        var checkout = attached.Value;
        return Result<BeginPreOrderCheckoutResult>.Success(new(checkout.CheckoutAttemptId,
            payment.ClientSecret, paymentGateway.PublishableKey, checkout.ProductName, checkout.Quantity,
            checkout.FullPrice, checkout.DepositAmount, checkout.BalanceAmount, checkout.PaymentAmount,
            checkout.ExpiresAtUtc));
    }
}
