namespace Diaspora.Modules.CommunityLife.Integration;

/// <summary>
/// An organization as another module sees it.
/// <para>
/// <b>Flat primitives and strings, deliberately.</b> A consumer reads this without referencing a
/// single Community Life type — no identifiers, no enums, no value objects — which is what makes
/// the contract safe to depend on: nothing here can be changed by a refactor inside an aggregate.
/// Enums are rendered as their names for exactly that reason.
/// </para>
/// </summary>
public sealed record OrganizationSummary
{
    public required Guid OrganizationId { get; init; }

    public required string Name { get; init; }

    /// <summary>The abbreviation, when there is one. What a chip renders.</summary>
    public string? ShortName { get; init; }

    public required string Slug { get; init; }

    public required string OrganizationTypeKey { get; init; }

    public string? Summary { get; init; }

    public required string CountryCode { get; init; }

    public string? CityName { get; init; }

    /// <summary>B4's area. The identifier every module already shares.</summary>
    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>B5 asset identifiers, never URLs. The consumer asks Media for the rendition.</summary>
    public Guid? LogoMediaId { get; init; }

    public Guid? CoverMediaId { get; init; }

    public required bool IsVerified { get; init; }

    /// <summary>Trust's own badge name, passed through and never parsed.</summary>
    public string? Badge { get; init; }

    public required int MemberCount { get; init; }

    public required int PublishedEventCount { get; init; }

    /// <summary>Whether members may currently join.</summary>
    public required bool AcceptsMembership { get; init; }

    public required bool IsFeatured { get; init; }

    /// <summary>The B7 provider behind it, when there is one.</summary>
    public Guid? ProviderId { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>The last time anything happened under it. What tells a living body from a page.</summary>
    public DateTimeOffset? LastActivityAt { get; init; }
}
