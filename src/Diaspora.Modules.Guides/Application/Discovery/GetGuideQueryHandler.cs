using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Authors;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.SharedKernel.Results;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// Serves the guide detail screen.
/// <para>
/// <b>A guide the caller may not see returns "not found", not "forbidden".</b> The difference
/// matters: a 403 confirms that a guide with that identifier exists, which is exactly what
/// somebody probing an unlisted draft wants to learn. A 404 tells them nothing.
/// </para>
/// <para>
/// Everything cross-cutting is hydrated here and handed to the mapper: provider cards from B7's
/// directory, the author's standing from this module's Trust projection, and Community's
/// authoritative comment count. Each fails soft on its own — a directory or comments outage
/// renders a slightly thinner page, never a failed one.
/// </para>
/// <para>
/// <b>The read records nothing.</b> A view is a fact about a reader and belongs to
/// <c>RecordGuideViewCommand</c>; a query that quietly incremented a counter would make every
/// cache warm-up and every crawler look like readership.
/// </para>
/// </summary>
public sealed class GetGuideQueryHandler : IQueryHandler<GetGuideQuery, GuideResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IGuideAuthorStandingRepository _standings;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideProviderResolver _providers;
    private readonly GuideDiscussionService _discussion;

    public GetGuideQueryHandler(
        IGuideRepository guides,
        IGuideAuthorStandingRepository standings,
        GuideAuthorizationService authorization,
        GuideProviderResolver providers,
        GuideDiscussionService discussion)
    {
        _guides = guides;
        _standings = standings;
        _authorization = authorization;
        _providers = providers;
        _discussion = discussion;
    }

    public async Task<Result<GuideResponse>> Handle(
        GetGuideQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var guide = await _guides.GetForReadAsync(GuideId.From(query.GuideId), cancellationToken);

        if (guide is null || !_authorization.CanSee(guide))
        {
            return Result.Failure<GuideResponse>(GuideErrors.NotFound);
        }

        var providerIds = guide.ProviderReferences
            .Select(reference => reference.ProviderId)
            .ToList();

        var summaries = await _providers.ResolveAsync(providerIds, cancellationToken);
        var standing = await _standings.GetByUserAsync(guide.AuthorId, cancellationToken);
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

        return Result.Success(
            GuideMapper.ToResponse(guide, summaries, ToAuthor(standing), isAuthor));
    }

    /// <summary>
    /// Trust's standing for the author, or null when the projection has never heard of them —
    /// which is the ordinary case for a new member, not an error.
    /// </summary>
    private static GuideAuthorResponse? ToAuthor(GuideAuthorStanding? standing) =>
        standing is null
            ? null
            : new GuideAuthorResponse
            {
                UserId = standing.UserId.Value,
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
