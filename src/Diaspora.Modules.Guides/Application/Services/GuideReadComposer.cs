using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Authors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Votes;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// Composes the guide detail read: the guide, its providers, its author's standing, name and
/// photo, the live comment count, and the reader's own save and vote.
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
    private readonly GuideBookmarkService _bookmarks;
    private readonly IGuideVoteRepository _votes;

    public GuideReadComposer(
        IGuideAuthorStandingRepository standings,
        GuideAuthorizationService authorization,
        GuideProviderResolver providers,
        GuideDiscussionService discussion,
        GuideAuthorNames authorNames,
        GuideBookmarkService bookmarks,
        IGuideVoteRepository votes)
    {
        _standings = standings;
        _authorization = authorization;
        _providers = providers;
        _discussion = discussion;
        _authorNames = authorNames;
        _bookmarks = bookmarks;
        _votes = votes;
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

        // The same for saves: the reader sees the count as of now, not as of the last
        // delivered save, and a guide saved before its count was kept reads right at once.
        if (await _bookmarks.CountAsync(guide.Id, cancellationToken) is { } saves)
        {
            guide.ApplyBookmarkCount(saves);
        }

        // Who reads the author's feedback (a revoked trust mark's reason): the author, and staff
        // reviewing the guide. A read detail, not a capability — staff change nothing through it.
        var seesAuthorFeedback = _authorization.IsStaff
            || (_authorization.CallerId is { } caller && guide.AuthorId == caller);

        var response = GuideMapper.ToResponse(
            guide, summaries, ToAuthor(guide.AuthorId, standing, profile), seesAuthorFeedback);

        return response with { Viewer = await ViewerAsync(guide, cancellationToken) };
    }

    /// <summary>The caller's save and vote, or null for a signed-out reader.</summary>
    private async Task<GuideViewerResponse?> ViewerAsync(Guide guide, CancellationToken cancellationToken)
    {
        if (_authorization.CallerId is not { } caller)
        {
            return null;
        }

        var saved = await _bookmarks.FilterSavedAsync(caller, [guide.Id], cancellationToken);
        var vote = await _votes.GetByGuideAndVoterAsync(guide.Id, caller, cancellationToken);

        return new GuideViewerResponse
        {
            IsSaved = saved.Contains(guide.Id.Value),
            Vote = vote?.Kind.ToString()
        };
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
