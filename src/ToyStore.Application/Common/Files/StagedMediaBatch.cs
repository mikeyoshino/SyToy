using System.Collections.ObjectModel;

namespace ToyStore.Application.Common.Files;

public sealed class StagedMediaBatch
{
    public StagedMediaBatch(string batchToken, IEnumerable<StagedMedia> media)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(batchToken);
        ArgumentNullException.ThrowIfNull(media);

        BatchToken = batchToken;
        Media = new ReadOnlyCollection<StagedMedia>(media.ToArray());
    }

    public string BatchToken { get; }

    public IReadOnlyList<StagedMedia> Media { get; }
}
