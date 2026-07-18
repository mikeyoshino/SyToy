using System.Collections.ObjectModel;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Products;

namespace ToyStore.UnitTests.Application;

public sealed class MediaStorageContractTests
{
    [Fact]
    public void ContractsExposeTheSingleProductImageLimitAndStableThaiFailures()
    {
        Assert.Equal(8, Product.MaximumImageCount);
        Assert.Equal("Media.EmptyBatch", MediaStorageErrors.EmptyBatch.Code);
        Assert.Equal("กรุณาเลือกรูปภาพอย่างน้อย 1 รูป", MediaStorageErrors.EmptyBatch.Message);
        Assert.Equal("Media.TooManyFiles", MediaStorageErrors.TooManyFiles.Code);
        Assert.Contains(
            Product.MaximumImageCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            MediaStorageErrors.TooManyFiles.Message);
        Assert.Equal(ErrorType.Validation, MediaStorageErrors.TooLarge.Type);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void UploadAllowsInvalidMimeToReachStableBoundaryValidation(string contentType)
    {
        using var content = new MemoryStream([0xff, 0xd8, 0xff]);

        var upload = new MediaUpload(content, contentType);

        Assert.Equal(contentType, upload.ContentType);
    }

    [Fact]
    public void StagedBatchSnapshotsCallerCollections()
    {
        var items = new List<StagedMedia>
        {
            new("aabbccddeeff00112233445566778899", "aabbccddeeff00112233445566778899/00112233445566778899aabbccddeeff.webp", "/media/aabbccddeeff00112233445566778899/00112233445566778899aabbccddeeff.webp", "image/webp", 16),
        };

        var batch = new StagedMediaBatch("aabbccddeeff00112233445566778899", items);
        items.Clear();

        Assert.Single(batch.Media);
        Assert.IsType<ReadOnlyCollection<StagedMedia>>(batch.Media);
    }

    [Fact]
    public void UploadDoesNotExposeClientFilenameOrDestinationPath()
    {
        var propertyNames = typeof(MediaUpload).GetProperties().Select(property => property.Name).ToArray();

        Assert.Equal(["Content", "ContentType"], propertyNames.Order());
        Assert.DoesNotContain(propertyNames, name => name.Contains("FileName", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("Path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EveryStorageOperationCarriesCancellationToken()
    {
        var operations = typeof(IFileStorage).GetMethods()
            .Concat(typeof(IFileStorageInitializer).GetMethods());

        Assert.All(operations, method => Assert.Contains(
            method.GetParameters(),
            parameter => parameter.ParameterType == typeof(CancellationToken)));
    }

    [Fact]
    public void ApplicationStorageBoundaryHasNoPhysicalOrAspNetUploadTypes()
    {
        var contractTypes = new[]
        {
            typeof(IFileStorage), typeof(IFileStorageInitializer), typeof(MediaUpload),
            typeof(StagedMediaBatch), typeof(StagedMedia), typeof(StoredMediaRead),
        };
        var signatures = contractTypes
            .SelectMany(type => type.GetMembers())
            .Select(member => member.ToString() ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(signatures, value => value.Contains("IFormFile", StringComparison.Ordinal));
        Assert.DoesNotContain(signatures, value => value.Contains("IBrowserFile", StringComparison.Ordinal));
        Assert.DoesNotContain(signatures, value => value.Contains("FileInfo", StringComparison.Ordinal));
        Assert.DoesNotContain(signatures, value => value.Contains("DirectoryInfo", StringComparison.Ordinal));
        Assert.DoesNotContain(
            typeof(IFileStorage).Assembly.GetTypes(),
            type => type.Name.Contains("FileRepository", StringComparison.OrdinalIgnoreCase));

        var applicationRoot = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "ToyStore.Application");
        var source = string.Join(
            '\n',
            Directory.GetFiles(applicationRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal))
                .Select(File.ReadAllText));
        Assert.DoesNotContain("System.IO.File", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Path.", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ToyStore.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ToyStore.sln.");
    }
}
