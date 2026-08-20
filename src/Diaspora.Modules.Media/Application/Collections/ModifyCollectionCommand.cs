using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Collections;

/// <summary>
/// Adds, removes, promotes or reorders assets in a collection.
/// <para>
/// One command for the four operations because they are one concern — the arrangement of a
/// collection — and four commands would be four handlers repeating the same load, the same
/// ownership check and the same projection. The operation is a discriminant rather than a
/// separate route because the client's editing UI treats them as one interaction.
/// </para>
/// </summary>
[Auditable]
public sealed record ModifyCollectionCommand : ICommand<MediaCollectionResponse>
{
    public required Guid CollectionId { get; init; }

    /// <summary><c>add</c>, <c>remove</c>, <c>reorder</c> or <c>promote</c>.</summary>
    public required string Operation { get; init; }

    /// <summary>The asset for <c>add</c>, <c>remove</c> and <c>promote</c>.</summary>
    public Guid? MediaId { get; init; }

    /// <summary>Every asset in the collection, in order, for <c>reorder</c>.</summary>
    public IReadOnlyList<Guid> MediaIds { get; init; } = [];

    /// <summary>Caption for an <c>add</c>.</summary>
    public string? Caption { get; init; }
}
