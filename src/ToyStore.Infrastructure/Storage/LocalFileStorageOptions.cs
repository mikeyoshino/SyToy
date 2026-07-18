namespace ToyStore.Infrastructure.Storage;

public sealed class LocalFileStorageOptions
{
    public const string SectionName = "Storage";

    public string RootPath { get; init; } = string.Empty;

    public TimeSpan StagingRetention { get; init; } = TimeSpan.FromHours(24);
}
