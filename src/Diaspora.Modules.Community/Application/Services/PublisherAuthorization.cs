using Diaspora.Contracts.Community.V1.Publishing;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// Decides who a new post is published as (20 Oct 2026).
/// <para>
/// <c>PublishAsOrganizationId</c> used to be stored as the publisher unchecked, so anybody could
/// post as any community — and the per-community moderation queue treats a post whose publisher is
/// a community as that community's content. The id is either a Community Programme community or a
/// Community Life organisation; both modules compose after this one, so each registers an
/// <see cref="IPublisherAuthority"/> and this asks every one. The first grant wins; ids are Guids,
/// so the two spaces do not collide. With no authority registered, nobody may publish as anything.
/// </para>
/// </summary>
public sealed class PublisherAuthorization
{
    // The publisher_display_name column's length (CommunityPersistenceConstants.NameLength).
    private const int MaxDisplayName = 200;

    private readonly IEnumerable<IPublisherAuthority> _authorities;

    public PublisherAuthorization(IEnumerable<IPublisherAuthority> authorities) =>
        _authorities = authorities;

    /// <summary>
    /// The publishing identity for a post by <paramref name="authorId"/>: personal when no body was
    /// named, the body when an authority grants it, and <see cref="CommunityErrors.PostPublisherForbidden"/>
    /// otherwise — never a quiet fallback to personal.
    /// </summary>
    public async Task<Result<PublishingIdentity>> ResolveAsync(
        UserId authorId, Guid? publishAs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorId);

        if (publishAs is not { } publisherId)
        {
            return Result.Success(PublishingIdentity.Personal());
        }

        if (publisherId == Guid.Empty)
        {
            return Result.Failure<PublishingIdentity>(CommunityErrors.PostPublisherForbidden);
        }

        foreach (var authority in _authorities)
        {
            var grant = await authority.AuthorizeAsync(authorId.Value, publisherId, cancellationToken);

            if (grant is null)
            {
                continue;
            }

            var type = grant.Kind == PublisherKind.Community
                ? PublishingIdentityType.Community
                : PublishingIdentityType.Organization;

            var name = grant.DisplayName.Length > MaxDisplayName
                ? grant.DisplayName[..MaxDisplayName]
                : grant.DisplayName;

            return Result.Success(PublishingIdentity.ForOrganization(type, publisherId, name));
        }

        return Result.Failure<PublishingIdentity>(CommunityErrors.PostPublisherForbidden);
    }
}
