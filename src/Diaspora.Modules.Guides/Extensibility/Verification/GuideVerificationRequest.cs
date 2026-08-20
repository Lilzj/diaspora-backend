namespace Diaspora.Modules.Guides.Extensibility.Verification;

/// <summary>
/// Everything a verification provider is told about a claim.
/// <para>
/// Flat primitives rather than the <c>Guide</c> aggregate, and that is the whole point of the
/// seam: a provider written against an editorial workflow needs a title, a jurisdiction and
/// the evidence — handing it the aggregate would couple it to every future change in B8's
/// domain and give it the ability to mutate what it was asked to check.
/// </para>
/// </summary>
public sealed record GuideVerificationRequest
{
    public required Guid GuideId { get; init; }

    /// <summary>The member submitting. Trust reviews people, not rows.</summary>
    public required Guid SubmittedBy { get; init; }

    /// <summary>The guide's author, who is accountable for the content.</summary>
    public required Guid AuthorId { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public required string CategoryKey { get; init; }

    /// <summary>Lower-case ISO 639-1.</summary>
    public required string Language { get; init; }

    /// <summary>The jurisdiction the knowledge applies to, so review can be routed by country.</summary>
    public string? CountryCode { get; init; }

    /// <summary>The published version number under review.</summary>
    public required int Version { get; init; }

    /// <summary>Supporting evidence — sources, credentials — as typed Media references.</summary>
    public IReadOnlyList<GuideVerificationEvidence> Evidence { get; init; } = [];

    /// <summary>Free text for the reviewer. Displayed, never interpreted.</summary>
    public string? Notes { get; init; }
}
