namespace ToyStore.Domain.Carts;

public enum CartRule
{
    CartIdentityRequired,
    CustomerIdentityRequired,
    CustomerIdentityTooLong,
    ProductRequired,
    ProductIdentityRequired,
    ProductMustBePublishedInStock,
    QuantityMustBePositive,
    QuantityExceedsLimit,
    CartItemNotFound,
    CartOwnershipMismatch,
    AuditInstantMustBeUtc,
    AuditTimeWentBackwards,
    ConcurrencyVersionMismatch,
    ConcurrencyVersionExhausted,
    OperationIdentityRequired,
    OperationTypeInvalid,
    OperationFingerprintInvalid,
    OperationResultVersionInvalid,
    OperationResultTotalInvalid,
    OperationResultDataInvalid,
}
