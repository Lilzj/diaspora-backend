namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One provider the guide points readers at. The identifier and the editorial text are the
/// guide's; the name, slug and badge are hydrated from B7's directory at read time — a
/// provider that renames itself or loses its badge is never misrepresented by an old guide.
/// The hydrated fields are null when the provider could not be resolved.
/// </summary>
public sealed record GuideProviderReferenceResponse
{
    public required Guid ProviderId { get; init; }

    /// <summary>What the guide calls this provider. Editorial, free text.</summary>
    public string? Role { get; init; }

    /// <summary>Why the guide recommends them, in the author's words.</summary>
    public string? Note { get; init; }

    public required int Order { get; init; }

    // ── Hydrated from B7 at read time ───────────────────────────────────────────────────────

    public string? Name { get; init; }

    public string? Slug { get; init; }

    public bool? IsVerified { get; init; }

    /// <summary>A Media identifier for the provider's logo. Never a URL.</summary>
    public Guid? LogoMediaId { get; init; }
}
