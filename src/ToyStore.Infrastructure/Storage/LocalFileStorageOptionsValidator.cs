using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ToyStore.Infrastructure.Storage;

internal sealed class LocalFileStorageOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<LocalFileStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, LocalFileStorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RootPath) ||
            !Path.IsPathFullyQualified(options.RootPath))
        {
            return ValidateOptionsResult.Fail(
                "Storage:RootPath must be an absolute persistent directory.");
        }

        if (options.StagingRetention <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail(
                "Storage:StagingRetention must be greater than zero.");
        }

        var configuredRoot = Path.GetFullPath(options.RootPath);
        if (StoragePathResolver.IsReparsePoint(configuredRoot))
        {
            return ValidateOptionsResult.Fail(
                "Storage:RootPath must not be a symbolic link or reparse point.");
        }

        var canonicalRoot = StoragePathResolver.ResolveExistingAliases(
            configuredRoot,
            includeLeaf: false);
        var canonicalContentRoot = StoragePathResolver.ResolveExistingAliases(
            environment.ContentRootPath,
            includeLeaf: true);
        if (environment.IsProduction() && IsWithin(canonicalRoot, canonicalContentRoot))
        {
            return ValidateOptionsResult.Fail(
                "Production Storage:RootPath must be outside the application deployment root.");
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsWithin(string candidate, string parent)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var normalizedCandidate = Path.TrimEndingDirectorySeparator(candidate);
        var normalizedParent = Path.TrimEndingDirectorySeparator(parent);
        return string.Equals(normalizedCandidate, normalizedParent, comparison) ||
               normalizedCandidate.StartsWith(
                   normalizedParent + Path.DirectorySeparatorChar,
                   comparison);
    }
}
