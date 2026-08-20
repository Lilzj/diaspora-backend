namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// A guide linked beside a posting — hydrated from B8 at read time.
/// <para>
/// The title and verification badge come from Guides on every read. A posting must never render
/// a link to a guide that has since been unpublished, and a copied title would do exactly that:
/// <see cref="Title"/> is null when the guide could not be resolved, and the client omits the
/// link.
/// </para>
/// </summary>
public sealed record JobGuideReferenceResponse
{
    public required Guid GuideId { get; init; }

    /// <summary>Why this guide is here. The employer's own words, when they gave any.</summary>
    public string? Label { get; init; }

    /// <summary>Whether the platform suggested this rather than the employer choosing it.</summary>
    public required bool IsAutomatic { get; init; }

    // ── Hydrated from B8. Null when the directory could not answer. ─────────────────────────

    public string? Title { get; init; }

    public string? Slug { get; init; }

    public string? Summary { get; init; }

    public bool? IsVerified { get; init; }
}
