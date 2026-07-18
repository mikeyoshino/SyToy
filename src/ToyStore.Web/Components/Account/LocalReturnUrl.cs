namespace ToyStore.Web.Components.Account;

public static class LocalReturnUrl
{
    public static string Normalize(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)
            || returnUrl.Contains('\\', StringComparison.Ordinal)
            || returnUrl.Any(char.IsControl)
            || HasMalformedPercentEncoding(returnUrl)
            || (!returnUrl.StartsWith('/')
                && Uri.TryCreate(returnUrl, UriKind.Absolute, out _))
            || returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return "/";
        }

        var normalized = returnUrl.StartsWith("~/", StringComparison.Ordinal)
            ? returnUrl[1..]
            : returnUrl.StartsWith('/')
                ? returnUrl
                : $"/{returnUrl}";

        return Uri.TryCreate(normalized, UriKind.Relative, out _) ? normalized : "/";
    }

    private static bool HasMalformedPercentEncoding(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%')
            {
                continue;
            }

            if (index + 2 >= value.Length
                || !Uri.IsHexDigit(value[index + 1])
                || !Uri.IsHexDigit(value[index + 2]))
            {
                return true;
            }

            index += 2;
        }

        return false;
    }
}
