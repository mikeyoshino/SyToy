using System.Runtime.CompilerServices;

namespace ToyStore.Application.Common.Diagnostics;

public static class ExceptionLogOwnership
{
    private static readonly ConditionalWeakTable<Exception, ApplicationLogMarker> Markers = new();

    public static void MarkApplicationLogged(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Markers.GetValue(exception, static _ => new ApplicationLogMarker());
    }

    public static bool IsApplicationLogged(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return Markers.TryGetValue(exception, out _);
    }

    private sealed class ApplicationLogMarker;
}
