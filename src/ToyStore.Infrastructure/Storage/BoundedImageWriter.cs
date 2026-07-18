using System.Buffers;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;

namespace ToyStore.Infrastructure.Storage;

internal static class BoundedImageWriter
{
    internal const int MaximumBytes = 5 * 1024 * 1024;
    private const int BufferSize = 64 * 1024;
    private const int MaximumSignatureBytes = 16;

    internal static async Task<Result<BoundedImageWrite>> CopyAsync(
        Stream input,
        Stream output,
        string declaredContentType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        if (!ImageSignatureValidator.IsSupportedContentType(declaredContentType))
        {
            return Result<BoundedImageWrite>.Failure(MediaStorageErrors.UnsupportedContentType);
        }

        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        var header = new byte[MaximumSignatureBytes];
        var headerLength = 0;
        var total = 0;

        try
        {
            while (total <= MaximumBytes)
            {
                var remaining = MaximumBytes + 1 - total;
                var count = await input.ReadAsync(
                    buffer.AsMemory(0, Math.Min(buffer.Length, remaining)),
                    cancellationToken);
                if (count == 0)
                {
                    break;
                }

                var signatureCount = Math.Min(count, MaximumSignatureBytes - headerLength);
                if (signatureCount > 0)
                {
                    buffer.AsSpan(0, signatureCount).CopyTo(header.AsSpan(headerLength));
                    headerLength += signatureCount;
                }

                total += count;
                if (total > MaximumBytes)
                {
                    return Result<BoundedImageWrite>.Failure(MediaStorageErrors.TooLarge);
                }

                await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            }

            var validation = ImageSignatureValidator.Validate(
                header.AsSpan(0, headerLength),
                declaredContentType);
            if (validation.IsFailure)
            {
                return Result<BoundedImageWrite>.Failure(validation.Error);
            }

            await output.FlushAsync(cancellationToken);
            return Result<BoundedImageWrite>.Success(
                new BoundedImageWrite(validation.Value, total));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}

internal readonly record struct BoundedImageWrite(ImageMediaType MediaType, long Length);
