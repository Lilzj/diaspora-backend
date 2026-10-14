using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Authors;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// Serves the guide detail screen from a deep link.
/// <para>
/// A slug that is not even well formed resolves as "not found" rather than as a validation
/// error: a bad address in somebody's chat message is a dead link, and telling its recipient
/// that their URL failed a regular expression helps nobody.
/// </para>
/// </summary>
public sealed class GetGuideBySlugQueryHandler
    : IQueryHandler<GetGuideBySlugQuery, GuideResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IGuideAuthorStandingRepository _standings;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideProviderResolver _providers;
    private readonly GuideDiscussionService _discussion;
    private readonly GuideAuthorNames _authorNames;

    public GetGuideBySlugQueryHandler(
        IGuideRepository guides,
        IGuideAuthorStandingRepository standings,
        GuideAuthorizationService authorization,
        GuideProviderResolver providers,
        GuideDiscussionService discussion,
        GuideAuthorNames authorNames)
    {
        _guides = guides;
        _standings = standings;
        _authorization = authorization;
        _providers = providers;
        _discussion = discussion;
        _authorNames = authorNames;
    }

    public async Task<Result<GuideResponse>> Handle(
        GetGuideBySlugQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var slug = GuideSlug.Create(query.Slug);

        if (slug.IsFailure)
        {
            return Result.Failure<GuideResponse>(GuideErrors.NotFound);
        }

        var guide = await _guides.GetBySlugAsync(slug.Value, cancellationToken);

        if (guide is null || !_authorization.CanSee(guide))
        {
            return Result.Failure<GuideResponse>(GuideErrors.NotFound);
        }

        var providerIds = guide.ProviderReferences
            .Select(reference => reference.ProviderId)
            .ToList();

        var summaries = await _providers.ResolveAsync(providerIds, cancellationToken);
        var standing = await _standings.GetByUserAsync(guide.AuthorId, cancellationToken);
        var authorName = await _authorNames.NameOfAsync(guide.AuthorId, cancellationToken);
        var comments = await _discussion.CountAsync(guide.Id, cancellationToken);

        // See GetGuideQueryHandler: the count is Community's, refreshed onto the untracked read
        // graph so the response is current without the read becoming a write.
        if (comments is { } count)
        {
            guide.ApplyCommentCount(count);
        }

        var isAuthor = _authorization.IsStaff
            || (_authorization.CallerId is { } caller && guide.AuthorId == caller);

        return Result.Success(
            GuideMapper.ToResponse(
                guide, summaries, ToAuthor(guide.AuthorId, standing, authorName), isAuthor));
    }

    /// <summary>
    /// Trust's standing for the author and their name from Identity. Null only when neither is
    /// known; a member Trust has not projected yet still gets a byline with their name.
    /// </summary>
    private static GuideAuthorResponse? ToAuthor(
        Diaspora.SharedKernel.Identity.UserId authorId, GuideAuthorStanding? standing, string? name) =>
        standing is null
            // A new member has no standing yet, but still has a name: the byline needs it.
            ? name is null
                ? null
                : new GuideAuthorResponse
                {
                    UserId = authorId.Value,
                    Name = name,
                    IsApprovedContributor = false,
                    IsCommunityGuide = false
                }
            : new GuideAuthorResponse
            {
                UserId = standing.UserId.Value,
                Name = name,
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
