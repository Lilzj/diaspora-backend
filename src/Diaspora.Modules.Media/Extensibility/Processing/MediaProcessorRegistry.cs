using Diaspora.Modules.Media.Domain.Enums;

namespace Diaspora.Modules.Media.Extensibility.Processing;

/// <summary>
/// Every registered processor, indexed by the operations it claims.
/// <para>
/// Ordered by <see cref="IMediaProcessor.Order"/>, and the first processor that does not
/// decline wins. That ordering is what lets a deployment install a better codec without
/// removing the built-in one: register at a lower order and it takes precedence, with the
/// built-in still behind it as a fallback.
/// </para>
/// <para>
/// Several processors may claim the same operation and that is not an error — unlike storage
/// providers and access policies, where a collision means an unanswerable question. Here it
/// means a preference list, which is exactly what a pluggable pipeline needs.
/// </para>
/// </summary>
public sealed class MediaProcessorRegistry
{
    private readonly IReadOnlyList<IMediaProcessor> _processors;

    public MediaProcessorRegistry(IEnumerable<IMediaProcessor> processors)
    {
        ArgumentNullException.ThrowIfNull(processors);

        _processors = processors
            .OrderBy(processor => processor.Order)
            .ThenBy(processor => processor.Name, StringComparer.Ordinal)
            .ToList();
    }

    public IReadOnlyList<IMediaProcessor> All => _processors;

    /// <summary>Processors that will attempt an operation on a kind, in preference order.</summary>
    public IReadOnlyList<IMediaProcessor> For(MediaKind kind, ProcessingOperation operation) =>
        _processors
            .Where(processor =>
                processor.SupportedOperations.Contains(operation) &&
                processor.CanProcess(kind, operation))
            .ToList();

    /// <summary>Whether anything at all can perform an operation on a kind.</summary>
    public bool Supports(MediaKind kind, ProcessingOperation operation) =>
        For(kind, operation).Count > 0;

    /// <summary>
    /// Every operation something can perform on a kind.
    /// <para>
    /// Read by the capabilities endpoint, which is how a client discovers that this deployment
    /// has no video transcoder before it offers a member a "post a video" button.
    /// </para>
    /// </summary>
    public IReadOnlyList<ProcessingOperation> SupportedOperationsFor(MediaKind kind) =>
        Enum.GetValues<ProcessingOperation>()
            .Where(operation => Supports(kind, operation))
            .ToList();

    /// <summary>Whether any registered processor is a malware scanner.</summary>
    public bool HasScanner => _processors.OfType<IVirusScanner>().Any();

    /// <summary>The scanners, in preference order.</summary>
    public IReadOnlyList<IVirusScanner> Scanners =>
        _processors.OfType<IVirusScanner>().ToList();
}
