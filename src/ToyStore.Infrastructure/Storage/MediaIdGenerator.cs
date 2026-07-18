using System.Security.Cryptography;

namespace ToyStore.Infrastructure.Storage;

internal interface IMediaIdGenerator
{
    string CreateId();
}

internal sealed class MediaIdGenerator : IMediaIdGenerator
{
    public string CreateId()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexStringLower(bytes);
    }
}
