namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>
/// Revise an organization. Omitted fields are left alone.
/// <para>
/// <see cref="BaseRevision"/> is the optimistic-concurrency token. Send back the revision you
/// loaded and a stale save is refused with a conflict you can merge from; omit it and the last
/// writer wins.
/// </para>
/// </summary>
public sealed record UpdateOrganizationRequest
{
    public int? BaseRevision { get; init; }

    public string? Name { get; init; }

    public string? ShortName { get; init; }

    public string? Summary { get; init; }

    public string? Description { get; init; }

    public string? OrganizationTypeKey { get; init; }

    public string? Mission { get; init; }

    public string? Vision { get; init; }

    public ContactDetailsRequest? Contact { get; init; }

    public SocialLinksRequest? SocialLinks { get; init; }

    public string? MembershipPolicy { get; init; }

    public string? MembershipRequirements { get; init; }

    public int? MembershipRenewalMonths { get; init; }

    public bool? IsMembershipPublic { get; init; }

    /// <summary><c>Private</c>, <c>Public</c>, <c>MembersOnly</c>, <c>Unlisted</c>.</summary>
    public string? Visibility { get; init; }

    public string? CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>
    /// The pin, in WGS84 degrees (-90..90). Optional; send both or neither.
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>The pin, in WGS84 degrees (-180..180). Optional; send both or neither.</summary>
    public double? Longitude { get; init; }

    public Guid? ProviderId { get; init; }

    public IReadOnlyList<string>? Languages { get; init; }

    public IReadOnlyList<string>? Services { get; init; }
}
