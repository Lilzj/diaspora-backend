using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.CommunityLife.V1.Events;

/// <summary>
/// An organization page was created.
/// <para>
/// Carries no content beyond its name and place, deliberately: a created organization is a draft
/// nobody can see, and a consumer that indexed it would put an unfinished page in front of members.
/// Everything downstream keys on <see cref="OrganizationPublishedIntegrationEvent"/>; this exists
/// for auditing and for counting directory growth.
/// </para>
/// </summary>
public sealed record OrganizationCreatedIntegrationEvent : IntegrationEvent
{
    public required Guid OrganizationId { get; init; }

    public required Guid CreatedByUserId { get; init; }

    public required string Name { get; init; }

    public required string Slug { get; init; }

    public required string OrganizationTypeKey { get; init; }

    public required string CountryCode { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
