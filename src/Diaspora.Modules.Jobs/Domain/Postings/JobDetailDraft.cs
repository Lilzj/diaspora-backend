namespace Diaspora.Modules.Jobs.Domain.Postings;

/// <summary>
/// One line as the editor sends it, before the aggregate validates the list as a set.
/// <para>
/// A domain-layer input type rather than a wire contract, so the aggregate's mutators take
/// something typed instead of a tuple, and so the application layer's mapper is the only place
/// that knows what the HTTP request looked like.
/// </para>
/// <para>
/// <see cref="LineId"/> is caller-supplied and may be empty for a new line. Supplying it on an
/// existing line is what lets a reorder update rows rather than delete and recreate them.
/// </para>
/// </summary>
public sealed record JobDetailDraft
{
    /// <summary>The line's identity. Empty for a line the editor just added.</summary>
    public Guid LineId { get; init; }

    public required string? Text { get; init; }

    /// <summary>Meaningful on a requirement only: a hard bar rather than a nice-to-have.</summary>
    public bool IsEssential { get; init; } = true;
}
