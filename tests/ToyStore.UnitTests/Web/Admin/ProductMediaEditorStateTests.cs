using Microsoft.AspNetCore.Components.Forms;
using ToyStore.Application.Products.ManageProducts;
using ToyStore.Web.Components.Admin.Primitives;

namespace ToyStore.UnitTests.Web.Admin;

public sealed class ProductMediaEditorStateTests
{
    [Fact]
    public void RetainedAndPendingImagesCanBeReorderedRemovedAndAreCappedAtEight()
    {
        var state = new ProductMediaEditorState();
        var retained = Enumerable.Range(0, 2).Select(index => new ProductManagementImage(
            Guid.NewGuid(), $"/image-{index}.webp", $"ภาพ {index}", index, index == 0));
        state.Load(retained);
        var files = Enumerable.Range(0, 8).Select(index => (IBrowserFile)new TestBrowserFile($"new-{index}.webp")).ToArray();
        state.AddFiles(files, files.Select(file => $"blob:{file.Name}").ToArray());

        Assert.Equal(8, state.Items.Count);
        var formerPrimary = state.Items[0];
        state.Move(0, 3);
        Assert.Same(formerPrimary, state.Items[3]);
        state.Remove(3);
        Assert.DoesNotContain(formerPrimary, state.Items);
        while (state.Items.Any(item => item.BrowserFile is not null))
        {
            state.Remove(Array.FindIndex(state.Items.ToArray(), item => item.BrowserFile is not null));
        }
        Assert.False(state.HasPendingSelection);
    }

    [Fact]
    public void NewPendingBatchCanReplaceOldBatchWithoutRemovingRetainedImages()
    {
        var state = new ProductMediaEditorState();
        var retainedId = Guid.NewGuid();
        state.Load([new ProductManagementImage(retainedId, "/retained.webp", "ภาพเดิม", 0, true)]);
        state.AddFiles([new TestBrowserFile("old.webp")], ["blob:old"]);

        state.RemovePending();
        state.AddFiles([new TestBrowserFile("new.webp")], ["blob:new"]);

        Assert.Equal(retainedId, state.Items[0].ProductImageId);
        Assert.Equal("new.webp", state.Items[1].BrowserFile?.Name);
        Assert.True(state.HasPendingSelection);
    }

    private sealed class TestBrowserFile(string name) : IBrowserFile
    {
        public string Name { get; } = name;
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => 1;
        public string ContentType => "image/webp";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => new MemoryStream([1]);
    }
}
