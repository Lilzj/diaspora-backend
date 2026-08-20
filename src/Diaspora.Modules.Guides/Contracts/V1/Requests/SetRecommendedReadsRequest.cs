namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Replaces what a curator says to read after one guide.
/// <para>
/// An empty list is valid and means "nothing follows this", which a curator sometimes genuinely
/// means and must be able to say without the record of who decided it disappearing.
/// </para>
/// </summary>
public sealed record SetRecommendedReadsRequest
{
    public IReadOnlyList<RecommendedReadInput> Reads { get; init; } = [];

    /// <summary>The revision the client was looking at.</summary>
    public int? Revision { get; init; }
}
