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

namespace Diaspora.Modules.Guides.Application.Authoring;

/// <summary>
/// Serves the author's preview.
/// <para>
/// <b>Author or staff, and nobody else.</b> Where the public detail read answers "not found" for
/// a guide the caller may not see, this one answers
/// <see cref="GuideErrors.DraftNotVisible"/> — the caller has already been told the guide exists
/// by holding its identifier from their own workspace, so hiding the reason would only confuse
/// them, and there is nothing to conceal from somebody who cannot get here without a permission.
/// </para>
/// <para>
/// The response is the ordinary detail shape, so what the author sees is literally what a reader
/// will see. The trust mark's reason is included, as it is for any author reading their own
/// guide: a revocation reason is feedback they can act on.
/// </para>
/// </summary>
public sealed class PreviewGuideQueryHandler : IQueryHandler<PreviewGuideQuery, GuideResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IGuideAuthorStandingRepository _standings;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideProviderResolver _providers;

    public PreviewGuideQueryHandler(
        IGuideRepository guides,
        IGuideAuthorStandingRepository standings,
        GuideAuthorizationService authorization,
        GuideProviderResolver providers)
    {
        _guides = guides;
        _standings = standings;
        _authorization = authorization;
        _providers = providers;
    }

    public async Task<Result<GuideResponse>> Handle(
        PreviewGuideQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var guide = await _guides.GetForReadAsync(GuideId.From(query.GuideId), cancellationToken);

        if (guide is null || guide.IsDeleted)
        {
            return Result.Failure<GuideResponse>(GuideErrors.NotFound);
        }

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<GuideResponse>(caller.Error);
        }

        if (!_authorization.IsStaff && guide.AuthorId != caller.Value)
        {
            return Result.Failure<GuideResponse>(GuideErrors.DraftNotVisible);
        }

        var providerIds = guide.ProviderReferences
            .Select(reference => reference.ProviderId)
            .ToList();

        var summaries = await _providers.ResolveAsync(providerIds, cancellationToken);
        var standing = await _standings.GetByUserAsync(guide.AuthorId, cancellationToken);

        // No comment count is fetched: an unpublished guide has no discussion, and asking
        // Community about content it has never seen is a round trip for a guaranteed zero.
        return Result.Success(
            GuideMapper.ToResponse(guide, summaries, ToAuthor(standing), forAuthor: true));
    }

    /// <summary>Trust's standing for the author, or null when the projection holds none.</summary>
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
