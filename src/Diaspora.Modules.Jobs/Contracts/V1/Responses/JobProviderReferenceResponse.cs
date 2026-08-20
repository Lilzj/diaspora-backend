namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// A provider a posting points at — hydrated from B7 at read time.
/// <para>
/// The name, badge and logo come from Providers on every read, never from a copy stored here, so
/// an agency that renames itself or loses its verification is never misrepresented by a posting
/// written six months ago. <see cref="Name"/> is null when the provider could not be resolved,
/// which renders as a bare reference rather than a failed page.
/// </para>
/// </summary>
public sealed record JobProviderReferenceResponse
{
    public required Guid ProviderId { get; init; }

    /// <summary>What this provider is to the posting — "recruiting agency", "visa support".</summary>
    public string? Role { get; init; }

    /// <summary>The employer's own note about them.</summary>
    public string? Note { get; init; }

    // ── Hydrated from B7. Null when the directory could not answer. ─────────────────────────

    public string? Name { get; init; }

    public string? Slug { get; init; }

    public bool? IsVerified { get; init; }

    public double? AverageRating { get; init; }

    public Guid? LogoMediaId { get; init; }
}
