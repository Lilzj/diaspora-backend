namespace Diaspora.Contracts.Community.V1.Publishing;

/// <summary>
/// Whether a member may publish under a body's name — a post whose publisher is that body.
/// <para>
/// Added 20 Oct 2026. <c>CreatePostRequest.PublishAsOrganizationId</c> was stored as the post's
/// publisher without asking anybody, so any member could post "as" any community, and the
/// per-community moderation queue (<see cref="Moderation.IPublisherReportQueue"/>) reads that
/// publisher as "this community's content". Community composes before both modules that own a body
/// a post can be published as, so it asks through this seam rather than referencing them.
/// </para>
/// <para>
/// <b>Two id spaces answer here.</b> Community Programme's communities (the
/// <c>/communities</c> routes, and the moderation queue's <c>{idOrSlug}</c>) and Community Life's
/// organisations (whose analytics and creator activity read the same publisher id). Each owning
/// module registers one implementation; Community asks every one and allows the post when one of
/// them grants it. With none registered, nobody may publish as anything.
/// </para>
/// </summary>
public interface IPublisherAuthority
{
    /// <summary>
    /// The grant when <paramref name="memberId"/> may publish as <paramref name="publisherId"/>,
    /// or null when this authority does not own that id or the member may not. A null never says
    /// which of the two it was.
    /// </summary>
    Task<PublisherGrant?> AuthorizeAsync(
        Guid memberId, Guid publisherId, CancellationToken cancellationToken = default);
}

/// <summary>What kind of body a post is published as.</summary>
public enum PublisherKind
{
    /// <summary>A Community Life organisation.</summary>
    Organization = 1,

    /// <summary>A Community Programme community.</summary>
    Community = 3
}

/// <summary>A granted publisher: its kind, and the name to show on the byline.</summary>
public sealed record PublisherGrant(PublisherKind Kind, string DisplayName);
