using Microsoft.AspNetCore.WebUtilities;

namespace ToyStore.Web.Components.Admin.Navigation;

public static class AdminRouteMatcher
{
    private static readonly string[] OrderDiscriminatorKeys = ["type", "status"];

    public static bool IsExactPath(string destination, string currentUri) =>
        string.Equals(
            PathAndQuery(destination).Path,
            PathAndQuery(currentUri).Path,
            StringComparison.OrdinalIgnoreCase);

    public static bool IsGroupActive(string routeGroup, string currentUri)
    {
        var path = PathAndQuery(currentUri).Path;
        return routeGroup switch
        {
            "dashboard" => path == "/admin",
            "catalog" => IsAtOrBelow(path, "/admin/products")
                || IsAtOrBelow(path, "/admin/brands")
                || IsAtOrBelow(path, "/admin/universes"),
            "inventory" => IsAtOrBelow(path, "/admin/inventory"),
            "orders" => IsAtOrBelow(path, "/admin/orders"),
            "notifications" => IsAtOrBelow(path, "/admin/notifications"),
            "reports" => IsAtOrBelow(path, "/admin/reports"),
            "settings" => IsAtOrBelow(path, "/admin/settings"),
            _ => false,
        };
    }

    public static bool IsContextActive(string destination, string currentUri)
    {
        var target = PathAndQuery(destination);
        var current = PathAndQuery(currentUri);
        if (!string.Equals(target.Path, current.Path, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expected = QueryHelpers.ParseQuery(target.Query);
        var actual = QueryHelpers.ParseQuery(current.Query);
        if (string.Equals(target.Path, "/admin/orders", StringComparison.OrdinalIgnoreCase))
        {
            return OrderDiscriminatorsMatch(expected, actual);
        }

        if (expected.Count == 0)
        {
            return true;
        }

        return expected.All(pair =>
            actual.TryGetValue(pair.Key, out var values)
            && pair.Value.All(value => values.Contains(value, StringComparer.OrdinalIgnoreCase)));
    }

    private static bool OrderDiscriminatorsMatch(
        IReadOnlyDictionary<string, Microsoft.Extensions.Primitives.StringValues> expected,
        IReadOnlyDictionary<string, Microsoft.Extensions.Primitives.StringValues> actual)
    {
        var expectedDiscriminators = UniqueOrderDiscriminators(expected);
        var actualDiscriminators = UniqueOrderDiscriminators(actual);
        if (expectedDiscriminators is null
            || actualDiscriminators is null
            || expectedDiscriminators.Count != actualDiscriminators.Count)
        {
            return false;
        }

        return expectedDiscriminators.All(pair =>
            actualDiscriminators.TryGetValue(pair.Key, out var value)
            && string.Equals(pair.Value, value, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, string>? UniqueOrderDiscriminators(
        IReadOnlyDictionary<string, Microsoft.Extensions.Primitives.StringValues> query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in OrderDiscriminatorKeys)
        {
            if (!query.TryGetValue(key, out var values))
            {
                continue;
            }

            if (values.Count != 1 || string.IsNullOrWhiteSpace(values[0]))
            {
                return null;
            }

            result.Add(key, values[0]!);
        }

        return result;
    }

    private static bool IsAtOrBelow(string path, string root) =>
        string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);

    private static (string Path, string Query) PathAndQuery(string uri)
    {
        var local = Uri.TryCreate(uri, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps)
            ? absolute.PathAndQuery
            : uri;
        var fragmentIndex = local.IndexOf('#', StringComparison.Ordinal);
        if (fragmentIndex >= 0)
        {
            local = local[..fragmentIndex];
        }

        var queryIndex = local.IndexOf('?', StringComparison.Ordinal);
        var path = queryIndex >= 0 ? local[..queryIndex] : local;
        var query = queryIndex >= 0 ? local[queryIndex..] : string.Empty;
        path = "/" + path.Trim().Trim('/');
        return (path.Length == 0 ? "/" : path, query);
    }
}
