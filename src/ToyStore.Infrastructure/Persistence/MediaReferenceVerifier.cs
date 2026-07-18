using Microsoft.EntityFrameworkCore;
using ToyStore.Application.Common.Files;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class MediaReferenceVerifier(
    IDbContextFactory<ApplicationDbContext> contextFactory)
    : IMediaReferenceVerifier
{
    public async Task<MediaReferenceVerification> VerifyAsync(
        TrustedMediaStorageKey storageKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(storageKey);

        try
        {
            await using var dbContext = await contextFactory.CreateDbContextAsync(
                cancellationToken);

            if (await dbContext.Brands
                    .AsNoTracking()
                    .AnyAsync(
                        brand => brand.Image != null
                            && brand.Image.StorageKey == storageKey.Value,
                        cancellationToken)
                || await dbContext.Universes
                    .AsNoTracking()
                    .AnyAsync(
                        universe => universe.Logo != null
                            && universe.Logo.StorageKey == storageKey.Value,
                        cancellationToken)
                || await dbContext.ProductImages
                    .AsNoTracking()
                    .AnyAsync(
                        image => image.StorageKey == storageKey.Value
                            || image.ThumbnailStorageKey == storageKey.Value,
                        cancellationToken))
            {
                return MediaReferenceVerification.Referenced;
            }

            return MediaReferenceVerification.Unreferenced;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return MediaReferenceVerification.Unavailable;
        }
    }
}
