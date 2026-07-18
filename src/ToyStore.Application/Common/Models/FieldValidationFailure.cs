namespace ToyStore.Application.Common.Models;

public sealed record FieldValidationFailure(string PropertyName, string ErrorMessage);
