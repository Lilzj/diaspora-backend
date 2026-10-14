namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>
/// Draft an organization.
/// <para>
/// Lands as a private draft whatever this says. Publishing is a separate act with its own
/// permission and its own readiness checks.
/// </para>
/// </summary>
public sealed record CreateOrganizationRequest
{
    public required string Name { get; init; }

    /// <summary>An abbreviation. What a chip renders when the full name will not fit.</summary>
    public string? ShortName { get; init; }

    public required string Summary { get; init; }

    public string? Description { get; init; }

    /// <summary>A key from the organization-type catalogue.</summary>
    public required string OrganizationTypeKey { get; init; }

    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    /// <summary>B4's area. Must be at neighbourhood level, and is resolved against B4.</summary>
    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>
    /// The pin, in WGS84 degrees (-90..90). Optional; send both or neither.
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>The pin, in WGS84 degrees (-180..180). Optional; send both or neither.</summary>
    public double? Longitude { get; init; }

    public string? Mission { get; init; }

    public string? Vision { get; init; }

    public ContactDetailsRequest? Contact { get; init; }

    public SocialLinksRequest? SocialLinks { get; init; }

    /// <summary><c>Open</c>, <c>RequestToJoin</c>, <c>InviteOnly</c>, <c>Closed</c>.</summary>
    public string? MembershipPolicy { get; init; }

    public string? MembershipRequirements { get; init; }

    /// <summary>
    /// Whether anybody may see who belongs. Defaults to true, and is a genuine safety setting when
    /// it is false: membership of a religious or political body is sensitive information in several
    /// of the countries this platform's members come from.
    /// </summary>
    public bool IsMembershipPublic { get; init; } = true;

    public IReadOnlyList<string>? Languages { get; init; }

    public IReadOnlyList<string>? Services { get; init; }

    /// <summary>The B7 provider behind it, when there is one.</summary>
    public Guid? ProviderId { get; init; }

    public IReadOnlyList<MediaAttachmentRequest>? Media { get; init; }
}
