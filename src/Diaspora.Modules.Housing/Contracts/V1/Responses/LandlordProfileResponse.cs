namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>A landlord, in full.</summary>
public sealed record LandlordProfileResponse
{
    public required Guid Id { get; init; }

    /// <summary>
    /// The member who owns this landlord profile, and therefore who a message about it reaches.
    /// <para>
    /// A landlord profile is not a user. Its identifier is a profile's, and the client was
    /// sending that as a conversation's recipient — so the thread was addressed to something
    /// that is not a person and nothing could deliver to it.
    /// </para>
    /// </summary>
    public Guid? OwnerUserId { get; init; }

    public required string Slug { get; init; }

    public required string DisplayName { get; init; }

    public required string Kind { get; init; }

    /// <summary>Present for a business. What Trust verified, as opposed to the trading name.</summary>
    public string? LegalName { get; init; }

    public required string PrimaryCountryCode { get; init; }

    public string? Bio { get; init; }

    public Guid? LogoMediaId { get; init; }

    public Guid? ProviderId { get; init; }

    /// <summary>Active, Suspended or Closed.</summary>
    public required string Status { get; init; }

    /// <summary>Present for the landlord and for staff.</summary>
    public string? SuspensionReason { get; init; }

    public required HousingTrustResponse Trust { get; init; }

    public required LandlordStatisticsResponse Statistics { get; init; }

    // ── Contact, redacted for this viewer ───────────────────────────────────────────────────

    /// <summary>Present when the landlord published it, or when the caller holds a viewing.</summary>
    public string? PhoneNumber { get; init; }

    public string? Email { get; init; }

    /// <summary>Survives redaction: it is already public, and it is how a member checks an agency is real.</summary>
    public string? WebsiteUrl { get; init; }

    public required bool IsOwner { get; init; }

    public required int Revision { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
