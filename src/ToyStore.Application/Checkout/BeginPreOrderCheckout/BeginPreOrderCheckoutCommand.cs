using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Checkout.BeginPreOrderCheckout;

public sealed record BeginPreOrderCheckoutCommand(
    Guid ProductId,
    int Quantity,
    Guid ClientRequestId,
    string RecipientName,
    string PhoneNumber,
    string AddressLine,
    string SubDistrict,
    string District,
    string Province,
    string PostalCode)
    : AuthorizedResultRequest<Result<BeginPreOrderCheckoutResult>>
{
    public override string RequiredPolicy => PolicyNames.CanUseCustomerCart;
    public override Result<BeginPreOrderCheckoutResult> CreateFailure(Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<BeginPreOrderCheckoutResult>.Failure(requestError, validationFailures);
}

public sealed record BeginPreOrderCheckoutResult(
    Guid CheckoutAttemptId,
    string ClientSecret,
    string PublishableKey,
    string ProductName,
    int Quantity,
    decimal FullPrice,
    decimal DepositAmount,
    decimal BalanceAmount,
    decimal PaymentAmount,
    DateTimeOffset ExpiresAtUtc);
