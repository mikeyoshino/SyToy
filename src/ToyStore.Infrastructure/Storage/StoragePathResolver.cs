namespace ToyStore.Infrastructure.Storage;

internal static class StoragePathResolver
{
    internal static string ResolveExistingAliases(string path, bool includeLeaf)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath)
            ?? throw new InvalidOperationException("The storage path has no filesystem root.");
        var segments = fullPath[root.Length..]
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        var limit = includeLeaf ? segments.Length : Math.Max(segments.Length - 1, 0);
        var current = root;

        for (var index = 0; index < segments.Length; index++)
        {
            var candidate = Path.Combine(current, segments[index]);
            if (index < limit && TryGetAttributes(candidate, out var attributes) &&
                (attributes & FileAttributes.ReparsePoint) != 0)
            {
                FileSystemInfo link = Directory.Exists(candidate)
                    ? new DirectoryInfo(candidate)
                    : new FileInfo(candidate);
                var target = link.ResolveLinkTarget(returnFinalTarget: true)
                    ?? throw new IOException("A configured storage path alias could not be resolved.");
                current = ResolveExistingAliases(target.FullName, includeLeaf: true);
            }
            else
            {
                current = candidate;
            }
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(current));
    }

    internal static bool IsReparsePoint(string path) =>
        TryGetAttributes(path, out var attributes) &&
        (attributes & FileAttributes.ReparsePoint) != 0;

    private static bool TryGetAttributes(string path, out FileAttributes attributes)
    {
        try
        {
            attributes = File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            attributes = default;
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            attributes = default;
            return false;
        }
    }
}
