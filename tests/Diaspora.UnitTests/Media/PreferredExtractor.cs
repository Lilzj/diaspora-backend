using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Extensibility.Processing;

namespace Diaspora.UnitTests.Media;

/// <summary>
/// Stands in for a codec-backed extractor a deployment installs.
/// <para>
/// Its whole purpose is its <see cref="Order"/>: at 10 it sorts ahead of the built-in
/// extractor's 1000, which is the mechanism a deployment uses to prefer a real library without
/// removing the fallback.
/// </para>
/// </summary>
internal sealed class PreferredExtractor : IMetadataExtractor
{
    public string Name => "preferred";

    public int Order => 10;

    public IReadOnlySet<ProcessingOperation> SupportedOperations { get; } =
        new HashSet<ProcessingOperation> { ProcessingOperation.ExtractMetadata };

    public bool CanProcess(MediaKind kind, ProcessingOperation operation) =>
        SupportedOperations.Contains(operation);

    public Task<MediaProcessingOutcome> ExtractAsync(
        MediaProcessingContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(MediaProcessingOutcome.Succeeded("preferred"));
}
