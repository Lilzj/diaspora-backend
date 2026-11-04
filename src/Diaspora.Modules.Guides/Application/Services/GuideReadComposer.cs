using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Authors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// Composes the guide detail read: the guide, its providers, its author's standing, name and
/// photo, and the live comment count.
/// <para>
/// <b>One composer for both doors.</b> <c>GET /guides/{id}</c> and <c>GET /guides/slug/{slug}</c>
/// each built this response themselves, line for line, so a field added to one was missing from
/// the other. They now load the guide and hand it here.
/// </para>
/// </summary>
public sealed class GuideReadComposer
{
    private readonly IGuideAuthorStandingRepository _standings;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideProviderResolver _providers;
    private readonly GuideDiscussionService _discussion;
    private readonly GuideAuthorNames _authorNames;

    public GuideReadComposer(
        IGuideAuthorStandingRepository standings,
        GuideAuthorizationService authorization,
        GuideProviderResolver providers,
        GuideDiscussionService discussion,
        GuideAuthorNames authorNames)
    {
        _standings = standings;
        _authorization = authorization;
        _providers = providers;
        _discussion = discussion;
        _authorNames = authorNames;
    }

    /// <summary>The response for a guide the caller may already see.</summary>
    public async Task<GuideResponse> ComposeAsync(Guide guide, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(guide);

        var providerIds = guide.ProviderReferences
            .Select(reference => reference.ProviderId)
            .ToList();

        var summaries = await _providers.ResolveAsync(providerIds, cancellationToken);
        var standing = await _standings.GetByUserAsync(guide.AuthorId, cancellationToken);
        var profile = await _authorNames.ProfileOfAsync(guide.AuthorId, cancellationToken);
        var comments = await _discussion.CountAsync(guide.Id, cancellationToken);

        // Community owns the count; the rollup on the aggregate is a cached copy of it. Applied
        // to the untracked read graph only, so the response is current without the read
        // becoming a write — no SaveChanges runs on this path, and the mutator raises no event
        // and bumps no revision by design.
        if (comments is { } count)
        {
            guide.ApplyCommentCount(count);
        }

        var isAuthor = _authorization.IsStaff
            || (_authorization.CallerId is { } caller && guide.AuthorId == caller);

        return GuideMapper.ToResponse(
            guide, summaries, ToAuthor(guide.AuthorId, standing, profile), isAuthor);
    }

    /// <summary>
    /// Trust's standing for the author, and their name and photo from Identity. Null only when
    /// neither is known: Trust having never heard of a new member is ordinary, and their name
    /// still shows.
    /// </summary>
    private static GuideAuthorResponse? ToAuthor(
        UserId authorId, GuideAuthorStanding? standing, MemberProfile? profile) =>
        standing is null
            // A new member has no standing yet, but still has a name: the byline needs it.
            ? profile is null
                ? null
                : new GuideAuthorResponse
                {
                    UserId = authorId.Value,
                    Name = profile.DisplayName,
                    AvatarUrl = profile.AvatarUrl,
                    IsApprovedContributor = false,
                    IsCommunityGuide = false
                }
            : new GuideAuthorResponse
            {
                UserId = standing.UserId.Value,
                Name = profile?.DisplayName,
                AvatarUrl = profile?.AvatarUrl,
                IsApprovedContributor = standing.IsApprovedContributor,
                IsCommunityGuide = standing.IsCommunityGuide,
                TrustLevel = standing.TrustLevel,
                Badges = standing.Badges
                    .Select(badge => new GuideAuthorBadgeResponse
                    {
                        Badge = badge.Badge,
                        CountryCode = badge.CountryCode,
                        AwardedAt = badge.AwardedAt
                    })
                    .ToList()
            };
}
