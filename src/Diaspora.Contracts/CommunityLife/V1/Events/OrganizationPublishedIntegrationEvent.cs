using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.CommunityLife.V1.Events;

/// <summary>
/// An organization became discoverable.
/// <para>
/// <b>The module's most consequential organization contract.</b> A directory index, a neighbourhood
/// hub and a search projection all key on it, which is why it carries a card payload rather than an
/// identifier: every one of those consumers would otherwise call straight back into Community Life
/// to render a single line.
/// </para>
/// <para>
/// <see cref="IsFirstPublication"/> distinguishes a new organization from one returning after an
/// archive. A consumer that announced both identically would tell members about a body they already
/// know.
/// </para>
/// </summary>
public sealed record OrganizationPublishedIntegrationEvent : IntegrationEvent
{
    public required Guid OrganizationId { get; init; }

    public required string Name { get; init; }

    public string? ShortName { get; init; }

    public required string Slug { get; init; }

    public required string OrganizationTypeKey { get; init; }

    public string? Summary { get; init; }

    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    public string? NeighborhoodName { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>A B5 asset identifier, never a URL. ADR-005.</summary>
    public Guid? LogoMediaId { get; init; }

    public Guid? CoverMediaId { get; init; }

    /// <summary>Languages you will be understood in, most prominent first.</summary>
    public IReadOnlyList<string> Languages { get; init; } = [];

    public required bool IsVerified { get; init; }

    public required int MemberCount { get; init; }

    public required bool IsFirstPublication { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>
    /// The organization's registered administrator — B13.1. The member who created it.
    /// <para>
    /// <b>Carried for personalization rather than for notification.</b> Publishing is the
    /// administrator's own act and warrants no message; what it warrants is a projected candidate
    /// that knows whose it is, which is what stops a recommendation surface offering an
    /// organization back to the person who runs it. Every other vertical's publication contract
    /// already named an owner and this one did not.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the organization could not be read.</para>
    /// </summary>
    public Guid? OrganizerUserId { get; init; }
}
