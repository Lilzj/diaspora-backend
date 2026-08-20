using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.Versions;
using Diaspora.SharedKernel.Results;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Versioning;

/// <summary>
/// Serves a guide's version history.
/// <para>
/// The guide is loaded first and checked for visibility, not because the version rows carry any
/// audience of their own but because they are the guide's: a history that answered for a draft
/// would leak the fact that the draft exists, and versions deliberately outlive the guide's own
/// soft delete.
/// </para>
/// </summary>
public sealed class ListGuideVersionsQueryHandler
    : IQueryHandler<ListGuideVersionsQuery, IReadOnlyList<GuideVersionSummaryResponse>>
{
    private readonly IGuideRepository _guides;
    private readonly IGuideVersionRepository _versions;
    private readonly GuideAuthorizationService _authorization;

    public ListGuideVersionsQueryHandler(
        IGuideRepository guides,
        IGuideVersionRepository versions,
        GuideAuthorizationService authorization)
    {
        _guides = guides;
        _versions = versions;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<GuideVersionSummaryResponse>>> Handle(
        ListGuideVersionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var guideId = GuideId.From(query.GuideId);
        var guide = await _guides.GetForReadAsync(guideId, cancellationToken);

        if (guide is null || !_authorization.CanSee(guide))
        {
            return Result.Failure<IReadOnlyList<GuideVersionSummaryResponse>>(
                GuideErrors.NotFound);
        }

        var limit = Math.Clamp(
            query.Limit ?? GuideLimits.MaxVersionsListed, 1, GuideLimits.MaxVersionsListed);

        var versions = await _versions.ListForGuideAsync(guideId, limit, cancellationToken);

        return Result.Success<IReadOnlyList<GuideVersionSummaryResponse>>(
            versions.Select(GuideVersionMapper.ToSummary).ToList());
    }
}
