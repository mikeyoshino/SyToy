namespace ToyStore.Application.Common.Files;

public interface IFileStorageInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}
