using ToyStore.Application.Common.Files;
using ToyStore.Infrastructure.Storage;

namespace ToyStore.UnitTests.Infrastructure.Storage;

public sealed class BoundedImageWriterTests
{
    [Fact]
    public async Task AllowsExactlyFiveMebibytesWithoutUsingStreamLength()
    {
        var bytes = CreateJpeg(BoundedImageWriter.MaximumBytes);
        await using var input = new NonSeekableShortReadStream(bytes, 7919);
        await using var output = new MemoryStream();

        var result = await BoundedImageWriter.CopyAsync(
            input,
            output,
            "image/jpeg",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BoundedImageWriter.MaximumBytes, result.Value.Length);
        Assert.Equal(bytes, output.ToArray());
        Assert.False(input.WasDisposed);
    }

    [Fact]
    public async Task RejectsFiveMebibytesPlusOneAndReadsNoMoreThanTheBoundary()
    {
        var bytes = CreateJpeg(BoundedImageWriter.MaximumBytes + 128);
        await using var input = new NonSeekableShortReadStream(bytes, 8192);
        await using var output = new MemoryStream();

        var result = await BoundedImageWriter.CopyAsync(
            input,
            output,
            "image/jpeg",
            CancellationToken.None);

        Assert.Equal(MediaStorageErrors.TooLarge, result.Error);
        Assert.Equal(BoundedImageWriter.MaximumBytes + 1, input.BytesRead);
        Assert.False(input.WasDisposed);
    }

    [Fact]
    public async Task LeavesCallerStreamsOpenWhenValidationFails()
    {
        await using var input = new NonSeekableShortReadStream([1, 2, 3], 1);
        await using var output = new MemoryStream();

        var result = await BoundedImageWriter.CopyAsync(
            input,
            output,
            "image/png",
            CancellationToken.None);

        Assert.Equal(MediaStorageErrors.InvalidSignature, result.Error);
        Assert.False(input.WasDisposed);
        Assert.True(output.CanWrite);
    }

    private static byte[] CreateJpeg(int length)
    {
        var result = new byte[length];
        result[0] = 0xff;
        result[1] = 0xd8;
        result[2] = 0xff;
        return result;
    }

    private sealed class NonSeekableShortReadStream(byte[] bytes, int maximumRead) : Stream
    {
        private int position;

        public bool WasDisposed { get; private set; }

        public int BytesRead => position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(Math.Min(buffer.Length, maximumRead), bytes.Length - position);
            bytes.AsMemory(position, count).CopyTo(buffer);
            position += count;
            return ValueTask.FromResult(count);
        }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
