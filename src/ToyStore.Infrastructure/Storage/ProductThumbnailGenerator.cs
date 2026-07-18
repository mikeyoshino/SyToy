using SkiaSharp;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;

namespace ToyStore.Infrastructure.Storage;

internal static class ProductThumbnailGenerator
{
    internal const int MaximumEdge = 960;
    internal const int MaximumSourceEdge = 12_000;
    internal const long MaximumSourcePixels = 60_000_000;
    internal const int WebpQuality = 84;

    internal static Result<long> Create(string sourcePath, string destinationPath)
    {
        using var source = SKBitmap.Decode(sourcePath);
        if (source is null
            || source.Width <= 0
            || source.Height <= 0
            || source.Width > MaximumSourceEdge
            || source.Height > MaximumSourceEdge
            || (long)source.Width * source.Height > MaximumSourcePixels)
        {
            return Result<long>.Failure(MediaStorageErrors.InvalidImageDimensions);
        }

        var ratio = Math.Min(1d, MaximumEdge / (double)Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * ratio));
        var height = Math.Max(1, (int)Math.Round(source.Height * ratio));
        using var resized = source.Resize(
            new SKImageInfo(width, height),
            new SKSamplingOptions(SKCubicResampler.Mitchell));
        if (resized is null)
        {
            return Result<long>.Failure(MediaStorageErrors.InvalidImageDimensions);
        }

        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, WebpQuality);
        if (encoded is null)
        {
            return Result<long>.Failure(MediaStorageErrors.InvalidImageDimensions);
        }

        using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        encoded.SaveTo(output);
        output.Flush(true);
        return Result<long>.Success(output.Length);
    }
}
