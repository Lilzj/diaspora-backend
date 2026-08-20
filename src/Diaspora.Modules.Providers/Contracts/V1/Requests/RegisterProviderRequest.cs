namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>
/// Registers a provider.
/// <para>
/// <b>Deliberately small.</b> A name, a type, a category and a way to be reached is the whole of
/// what registration asks for; everything else is edited afterwards on a screen built for it. A
/// registration form that demanded a gallery, opening hours and a service list would be
/// abandoned by exactly the small providers this platform exists to bring online.
/// </para>
/// <para>
/// The provider is created as a draft. Publishing is a separate, explicit act, so nobody
/// discovers a half-written listing of theirs already live.
/// </para>
/// </summary>
public sealed record RegisterProviderRequest
{
    public required string Name { get; init; }

    /// <summary>The <c>ProviderType</c> name — <c>Business</c>, <c>Church</c>, <c>Individual</c>.</summary>
    public required string ProviderType { get; init; }

    /// <summary>
    /// A category identifier, slug, name or legacy alias. Resolved through the catalogue, so a
    /// client still sending the frontend's old <c>OrgCategory</c> strings keeps working.
    /// </summary>
    public required string Category { get; init; }

    public string? Description { get; init; }

    public string? Tagline { get; init; }

    /// <summary>The <c>ServiceDeliveryMode</c> name. Defaults to the type's own default.</summary>
    public string? DeliveryMode { get; init; }

    /// <summary>Chosen by the caller, or derived from the name when omitted.</summary>
    public string? Slug { get; init; }

    // ── Geography. Every field optional; resolved by Location. ──────────────────────────────

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    public string? CityName { get; init; }

    public string? AddressLine { get; init; }

    public string? PostalCode { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    // ── Contact. At least one is needed to publish, though not to register. ─────────────────

    public string? Phone { get; init; }

    public string? WhatsApp { get; init; }

    public string? Email { get; init; }

    public string? Website { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];
}
