using ToyStore.Application.Common.Files;
using ToyStore.Infrastructure.Storage;

namespace ToyStore.UnitTests.Infrastructure.Storage;

public sealed class ImageSignatureValidatorTests
{
    public static TheoryData<byte[], string, string> ValidImages => new()
    {
        { [0xff, 0xd8, 0xff, 0x00], "image/jpeg", ".jpg" },
        { [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a], "IMAGE/PNG", ".png" },
        { WebP("VP8 "), "image/webp", ".webp" },
        { WebP("VP8L"), "image/webp", ".webp" },
        { WebP("VP8X"), "image/webp", ".webp" },
    };

    [Theory]
    [MemberData(nameof(ValidImages))]
    public void DetectsApprovedCanonicalFormats(byte[] bytes, string declaredType, string extension)
    {
        var result = ImageSignatureValidator.Validate(bytes, declaredType);

        Assert.True(result.IsSuccess);
        Assert.Equal(declaredType.ToLowerInvariant(), result.Value.ContentType);
        Assert.Equal(extension, result.Value.Extension);
    }

    [Theory]
    [InlineData("image/jpg")]
    [InlineData("image/jpeg; charset=binary")]
    [InlineData(" image/jpeg")]
    [InlineData("image/gif")]
    [InlineData("")]
    [InlineData(" ")]
    public void RejectsNonCanonicalDeclaredMime(string declaredType)
    {
        var result = ImageSignatureValidator.Validate([0xff, 0xd8, 0xff], declaredType);

        Assert.Equal(MediaStorageErrors.UnsupportedContentType, result.Error);
    }

    [Fact]
    public void DistinguishesInvalidSignatureFromMimeMismatch()
    {
        var invalid = ImageSignatureValidator.Validate([0x47, 0x49, 0x46], "image/jpeg");
        var mismatch = ImageSignatureValidator.Validate(
            [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a],
            "image/jpeg");

        Assert.Equal(MediaStorageErrors.InvalidSignature, invalid.Error);
        Assert.Equal(MediaStorageErrors.ContentTypeMismatch, mismatch.Error);
    }

    [Theory]
    [MemberData(nameof(TruncatedImages))]
    public void RejectsEmptyAndTruncatedSignatures(byte[] bytes, string mime)
    {
        var result = ImageSignatureValidator.Validate(bytes, mime);

        Assert.Equal(MediaStorageErrors.InvalidSignature, result.Error);
    }

    public static TheoryData<byte[], string> TruncatedImages => new()
    {
        { [], "image/jpeg" },
        { [0xff, 0xd8], "image/jpeg" },
        { [0x89, 0x50, 0x4e, 0x47], "image/png" },
        { [0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50], "image/webp" },
    };

    private static byte[] WebP(string marker) =>
    [
        0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0,
        0x57, 0x45, 0x42, 0x50,
        .. System.Text.Encoding.ASCII.GetBytes(marker),
    ];
}
