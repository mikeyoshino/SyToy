using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Checkout.BeginInStockCheckout;

public sealed record BeginInStockCheckoutCommand(
    Guid ClientRequestId,
    string RecipientName,
    string PhoneNumber,
    string AddressLine,
    string SubDistrict,
    string District,
    string Province,
    string PostalCode)
    : AuthorizedResultRequest<Result<BeginInStockCheckoutResult>>
{
    public override string RequiredPolicy => PolicyNames.CanUseCustomerCart;
    public override Result<BeginInStockCheckoutResult> CreateFailure(Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<BeginInStockCheckoutResult>.Failure(requestError, validationFailures);
}

public sealed record BeginInStockCheckoutResult(
    Guid CheckoutAttemptId,
    string ClientSecret,
    string PublishableKey,
    IReadOnlyList<PreparedInStockCheckoutItem> Items,
    decimal ShippingAmount,
    decimal PaymentAmount,
    DateTimeOffset ExpiresAtUtc);
