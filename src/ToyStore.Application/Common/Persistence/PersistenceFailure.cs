using ToyStore.Application.Common.Messaging;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Common.Persistence;

public enum PersistenceFailureTarget
{
    Request,
    Brand,
    Universe,
    Character,
    StockMovement,
    Product,
    CartOperation,
}

public enum PersistenceFailureKind
{
    DuplicateDisplayName,
    DuplicateEnglishName,
    ConcurrencyConflict,
    DuplicateName,
    DuplicateOperation,
}

public sealed record PersistenceFailure(
    PersistenceFailureTarget Target,
    PersistenceFailureKind Kind);

public interface IPersistenceFailureClassifier
{
    PersistenceFailure? Classify(Exception exception);
}

public interface IPersistenceFailureResultRequest<out TResponse>
    : IResultRequest<TResponse>
{
    Error? MapPersistenceFailure(PersistenceFailure failure);
}
