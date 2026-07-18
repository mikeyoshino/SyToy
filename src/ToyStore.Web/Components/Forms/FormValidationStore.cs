using System.Reflection;
using Microsoft.AspNetCore.Components.Forms;
using ToyStore.Application.Common.Models;

namespace ToyStore.Web.Components.Forms;

public sealed class FormValidationStore : IDisposable
{
    private readonly EditContext editContext;
    private readonly ValidationMessageStore messages;
    private bool disposed;

    public FormValidationStore(EditContext editContext)
    {
        this.editContext = editContext ?? throw new ArgumentNullException(nameof(editContext));
        messages = new ValidationMessageStore(editContext);
        editContext.OnFieldChanged += HandleFieldChanged;
    }

    public void Display(IEnumerable<FieldValidationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        messages.Clear();

        foreach (var failure in failures)
        {
            if (failure is null || string.IsNullOrWhiteSpace(failure.ErrorMessage))
            {
                continue;
            }

            messages.Add(ResolveField(failure.PropertyName), failure.ErrorMessage);
        }

        editContext.NotifyValidationStateChanged();
    }

    public void Clear()
    {
        messages.Clear();
        editContext.NotifyValidationStateChanged();
    }

    private void HandleFieldChanged(object? sender, FieldChangedEventArgs args)
    {
        messages.Clear(args.FieldIdentifier);
        editContext.NotifyValidationStateChanged();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        editContext.OnFieldChanged -= HandleFieldChanged;
    }

    private FieldIdentifier ResolveField(string? propertyPath)
    {
        if (string.IsNullOrWhiteSpace(propertyPath))
        {
            return ModelLevelField();
        }

        var segments = propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return ModelLevelField();
        }

        object parent = editContext.Model;
        for (var index = 0; index < segments.Length - 1; index++)
        {
            var property = FindPublicProperty(parent.GetType(), segments[index]);
            var next = property?.GetValue(parent);
            if (next is null)
            {
                return ModelLevelField();
            }

            parent = next;
        }

        var fieldName = segments[^1];
        return FindPublicProperty(parent.GetType(), fieldName) is null
            ? ModelLevelField()
            : new FieldIdentifier(parent, fieldName);
    }

    private FieldIdentifier ModelLevelField() => new(editContext.Model, string.Empty);

    private static PropertyInfo? FindPublicProperty(Type type, string propertyName) =>
        type.GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
}
