namespace ToyStore.Web.Components.Forms;

internal static class FieldAttributeBuilder
{
    public static Dictionary<string, object> Create(
        IReadOnlyDictionary<string, object>? additionalAttributes,
        string id,
        string cssClass,
        bool required,
        bool disabled,
        bool invalid,
        string describedBy,
        params (string Name, object? Value)[] attributes)
    {
        var result = additionalAttributes is null
            ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object>(additionalAttributes, StringComparer.OrdinalIgnoreCase);

        var additionalClass = result.TryGetValue("class", out var classValue)
            ? Convert.ToString(classValue, System.Globalization.CultureInfo.InvariantCulture)
            : null;
        var additionalDescription = result.TryGetValue("aria-describedby", out var describedByValue)
            ? Convert.ToString(describedByValue, System.Globalization.CultureInfo.InvariantCulture)
            : null;

        result["id"] = id;
        result["class"] = string.IsNullOrWhiteSpace(additionalClass)
            ? cssClass
            : $"{cssClass} {additionalClass}";
        result["aria-describedby"] = string.IsNullOrWhiteSpace(additionalDescription)
            ? describedBy
            : $"{additionalDescription} {describedBy}";

        if (required)
        {
            result["aria-required"] = "true";
        }
        else
        {
            result.Remove("aria-required");
        }

        if (disabled)
        {
            result["disabled"] = true;
        }
        else
        {
            result.Remove("disabled");
        }

        if (invalid)
        {
            result["aria-invalid"] = "true";
        }
        else
        {
            result.Remove("aria-invalid");
        }

        foreach (var (name, value) in attributes)
        {
            if (value is null || value is string stringValue && string.IsNullOrWhiteSpace(stringValue))
            {
                result.Remove(name);
            }
            else
            {
                result[name] = value;
            }
        }

        return result;
    }
}
