using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ToyStore.Application.Common.Files;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class MediaEndpointTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task ServesCommittedMediaWithImmutableAndConditionalHeaders()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        var staged = await StageAndCommitAsync(factory.Services);

        using var response = await client.GetAsync(
            staged.Media[0].PublicRelativeUrl,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(6, response.Content.Headers.ContentLength);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        var cacheControl = response.Headers.CacheControl
            ?? throw new Xunit.Sdk.XunitException("Expected Cache-Control header.");
        Assert.Contains("immutable", cacheControl.Extensions.Select(value => value.Name));
        Assert.NotNull(response.Headers.ETag);
        Assert.NotNull(response.Content.Headers.LastModified);
        Assert.Equal(new byte[] { 0xff, 0xd8, 0xff, 1, 2, 3 }, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));

        using var conditional = new HttpRequestMessage(HttpMethod.Get, staged.Media[0].PublicRelativeUrl);
        conditional.Headers.IfNoneMatch.Add(response.Headers.ETag);
        using var notModified = await client.SendAsync(
            conditional,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);
    }

    [Fact]
    public async Task SupportsHeadAndByteRanges()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        var staged = await StageAndCommitAsync(factory.Services);
        var url = staged.Media[0].PublicRelativeUrl;

        using var head = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Head, url),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, head.StatusCode);
        Assert.Equal(6, head.Content.Headers.ContentLength);
        Assert.Empty(await head.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));

        using var rangeRequest = new HttpRequestMessage(HttpMethod.Get, url);
        rangeRequest.Headers.Range = new RangeHeaderValue(1, 3);
        using var range = await client.SendAsync(rangeRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.PartialContent, range.StatusCode);
        Assert.Equal(new byte[] { 0xd8, 0xff, 1 }, await range.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));

        using var invalidRangeRequest = new HttpRequestMessage(HttpMethod.Get, url);
        invalidRangeRequest.Headers.Range = new RangeHeaderValue(99, 100);
        using var invalidRange = await client.SendAsync(
            invalidRangeRequest,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, invalidRange.StatusCode);
    }

    [Theory]
    [InlineData("/media/%252e%252e/secret.jpg")]
    [InlineData("/media/aabbccddeeff00112233445566778899/file.svg")]
    [InlineData("/media/aabbccddeeff00112233445566778899%252f..%252fsecret.jpg")]
    public async Task InvalidOrMissingKeysReturnNotFound(string url)
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var response = await client.GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task MutationMethodsAreRejected(string method)
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        var staged = await StageAndCommitAsync(factory.Services);
        using var request = new HttpRequestMessage(new HttpMethod(method), staged.Media[0].PublicRelativeUrl);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(["GET", "HEAD"], response.Content.Headers.Allow.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task CompletedResponseDisposesProviderOwnedStream()
    {
        var stream = new TrackingStream([1, 2, 3]);
        var boundary = new StubFileStorage((_, _) => Task.FromResult<StoredMediaRead?>(new(
            stream,
            "image/jpeg",
            3,
            new DateTimeOffset(2026, 7, 18, 0, 0, 0, TimeSpan.Zero),
            "\"tracked\"")));
        await using var factory = new MediaBoundaryFactory(postgreSql.ConnectionString, boundary);
        using var client = factory.CreateClient();

        using (var response = await client.GetAsync(
                   ValidMediaUrl,
                   TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            _ = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        }

        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task AbortedCopyDisposesProviderOwnedStream()
    {
        var stream = new ThrowingTrackingStream();
        var boundary = new StubFileStorage((_, _) => Task.FromResult<StoredMediaRead?>(new(
            stream,
            "image/jpeg",
            3,
            new DateTimeOffset(2026, 7, 18, 0, 0, 0, TimeSpan.Zero),
            "\"aborted\"")));
        await using var factory = new MediaBoundaryFactory(postgreSql.ConnectionString, boundary);
        using var client = factory.CreateClient();

        await Assert.ThrowsAnyAsync<Exception>(() => client.GetAsync(
            ValidMediaUrl,
            TestContext.Current.CancellationToken));

        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task UnexpectedEndpointFailureDoesNotLeakPhysicalStoragePath()
    {
        const string physicalPath = "/var/lib/toystore/uploads/files/secret.jpg";
        var boundary = new StubFileStorage((_, _) =>
            throw new IOException("Cannot read " + physicalPath));
        await using var factory = new MediaBoundaryFactory(postgreSql.ConnectionString, boundary);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            ValidMediaUrl,
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain(physicalPath, body, StringComparison.Ordinal);
        Assert.DoesNotContain("Cannot read", body, StringComparison.Ordinal);
    }

    private static async Task<StagedMediaBatch> StageAndCommitAsync(IServiceProvider services)
    {
        var storage = services.GetRequiredService<IFileStorage>();
        var stage = await storage.StageAsync(
            [new MediaUpload(new MemoryStream([0xff, 0xd8, 0xff, 1, 2, 3]), "image/jpeg")],
            TestContext.Current.CancellationToken);
        Assert.True(stage.IsSuccess);
        await storage.CommitAsync(stage.Value, TestContext.Current.CancellationToken);
        return stage.Value;
    }

    private const string ValidMediaUrl =
        "/media/aabbccddeeff00112233445566778899/00112233445566778899aabbccddeeff.jpg";

    private sealed class MediaBoundaryFactory(string connectionString, IFileStorage boundary)
        : ToyStoreWebApplicationFactory(connectionString)
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IFileStorage>();
                services.AddSingleton(boundary);
            });
        }
    }

    private sealed class StubFileStorage(
        Func<string, CancellationToken, Task<StoredMediaRead?>> openRead) : IFileStorage
    {
        public Task<ToyStore.Application.Common.Models.Result<StagedMediaBatch>> StageAsync(
            IReadOnlyCollection<MediaUpload> uploads,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task CommitAsync(StagedMediaBatch batch, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DiscardStagingAsync(string batchToken, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DeleteCommittedAsync(
            IReadOnlyCollection<string> storageKeys,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StoredMediaRead?> OpenReadAsync(
            string storageKey,
            CancellationToken cancellationToken) => openRead(storageKey, cancellationToken);

        public Task CleanupStagingAsync(
            DateTimeOffset olderThanUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private class TrackingStream(byte[] bytes) : MemoryStream(bytes)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            Disposed = true;
            await base.DisposeAsync();
        }
    }

    private sealed class ThrowingTrackingStream() : TrackingStream([1, 2, 3])
    {
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new OperationCanceledException("aborted copy"));
    }
}
