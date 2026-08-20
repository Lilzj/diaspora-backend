namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One body whose statements this platform treats as official.
/// <para>
/// <b>No owner, no rating, no review count — and their absence is the design.</b> An authority is
/// managed content about an institution that will never hold an account here. Nothing on this
/// response invites a member to claim it, rate it or dispute it, because none of those is a thing you
/// do to an immigration department.
/// </para>
/// </summary>
public sealed record IssuingAuthorityResponse
{
    public required Guid IssuingAuthorityId { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2 — the country this body issues for.</summary>
    public required string CountryCode { get; init; }

    public required string Name { get; init; }

    /// <summary>The acronym members and staff actually use.</summary>
    public string? ShortName { get; init; }

    /// <summary>PascalCase — <c>ImmigrationDepartment</c>, <c>Embassy</c>.</summary>
    public required string AuthorityType { get; init; }

    public string? Description { get; init; }

    public string? Website { get; init; }

    public string? ContactEmail { get; init; }

    public string? ContactPhone { get; init; }

    /// <summary>Whether Trust has confirmed it. The gate every publication passes.</summary>
    public required bool IsVerified { get; init; }

    public DateTimeOffset? VerifiedAt { get; init; }

    /// <summary>Whether notices may still name it.</summary>
    public required bool IsActive { get; init; }

    /// <summary>The concurrency token to send back on an edit. Editors only.</summary>
    public int? Revision { get; init; }
}
