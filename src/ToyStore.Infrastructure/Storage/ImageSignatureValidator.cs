using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;

namespace ToyStore.Infrastructure.Storage;

internal static class ImageSignatureValidator
{
    private static ReadOnlySpan<byte> PngSignature =>
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    internal static Result<ImageMediaType> Validate(
        ReadOnlySpan<byte> header,
        string declaredContentType)
    {
        if (!TryCanonicalize(declaredContentType, out var canonicalDeclared))
        {
            return Result<ImageMediaType>.Failure(MediaStorageErrors.UnsupportedContentType);
        }

        ImageMediaType? detected = null;
        if (header.Length >= 3 &&
            header[0] == 0xff &&
            header[1] == 0xd8 &&
            header[2] == 0xff)
        {
            detected = new ImageMediaType("image/jpeg", ".jpg");
        }
        else if (header.Length >= PngSignature.Length &&
                 header[..PngSignature.Length].SequenceEqual(PngSignature))
        {
            detected = new ImageMediaType("image/png", ".png");
        }
        else if (IsWebP(header))
        {
            detected = new ImageMediaType("image/webp", ".webp");
        }
        else if (LooksLikeAnotherApprovedFormat(header, canonicalDeclared))
        {
            return Result<ImageMediaType>.Failure(MediaStorageErrors.ContentTypeMismatch);
        }

        if (detected is null)
        {
            return Result<ImageMediaType>.Failure(MediaStorageErrors.InvalidSignature);
        }

        return !string.Equals(
            detected.Value.ContentType,
            canonicalDeclared,
            StringComparison.Ordinal)
            ? Result<ImageMediaType>.Failure(MediaStorageErrors.ContentTypeMismatch)
            : Result<ImageMediaType>.Success(detected.Value);
    }

    internal static bool IsSupportedContentType(string value) =>
        TryCanonicalize(value, out _);

    private static bool TryCanonicalize(string value, out string canonical)
    {
        canonical = value switch
        {
            var current when string.Equals(current, "image/jpeg", StringComparison.OrdinalIgnoreCase)
                => "image/jpeg",
            var current when string.Equals(current, "image/png", StringComparison.OrdinalIgnoreCase)
                => "image/png",
            var current when string.Equals(current, "image/webp", StringComparison.OrdinalIgnoreCase)
                => "image/webp",
            _ => string.Empty,
        };

        return canonical.Length != 0;
    }

    private static bool IsWebP(ReadOnlySpan<byte> header)
    {
        if (header.Length < 16 ||
            !header[..4].SequenceEqual("RIFF"u8) ||
            !header.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return false;
        }

        var marker = header.Slice(12, 4);
        return marker.SequenceEqual("VP8 "u8) ||
               marker.SequenceEqual("VP8L"u8) ||
               marker.SequenceEqual("VP8X"u8);
    }

    private static bool LooksLikeAnotherApprovedFormat(
        ReadOnlySpan<byte> header,
        string declaredContentType) =>
        (!string.Equals(declaredContentType, "image/jpeg", StringComparison.Ordinal) &&
         header.Length >= 3 && header[..3].SequenceEqual(new byte[] { 0xff, 0xd8, 0xff })) ||
        (!string.Equals(declaredContentType, "image/png", StringComparison.Ordinal) &&
         header.Length >= PngSignature.Length &&
         header[..PngSignature.Length].SequenceEqual(PngSignature)) ||
        (!string.Equals(declaredContentType, "image/webp", StringComparison.Ordinal) &&
         IsWebP(header));
}

internal readonly record struct ImageMediaType(string ContentType, string Extension);
