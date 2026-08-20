using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.CommunityLife.V1.Events;

/// <summary>
/// A Media asset was attached to an event or an organization.
/// <para>
/// One contract for both subjects, with <see cref="SubjectType"/> telling them apart, because every
/// consumer of it — the media lifecycle sweep above all — does exactly the same thing with either.
/// </para>
/// <para>
/// An identifier and a role. Never a URL: Media owns the bytes, the variants and the signed link,
/// and a consumer that cached one would serve a broken image the first time B5 rotated its storage.
/// </para>
/// </summary>
public sealed record CommunityLifeMediaAttachedIntegrationEvent : IntegrationEvent
{
    /// <summary>What the asset was attached to — <c>Event</c> or <c>Organization</c>.</summary>
    public required string SubjectType { get; init; }

    public required Guid SubjectId { get; init; }

    public Guid? OrganizationId { get; init; }

    public required Guid MediaId { get; init; }

    /// <summary>The role, as its name — <c>Cover</c>, <c>Logo</c>, <c>Gallery</c>, and the rest.</summary>
    public required string Role { get; init; }

    public required DateTimeOffset AttachedAt { get; init; }
}
