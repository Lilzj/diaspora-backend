using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Results;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// Serves the language switcher.
/// <para>
/// Guides sharing a <c>TranslationGroupId</c> are editions of each other, and the group
/// identifier defaults to a guide's own id — so "the group" always exists and joining one is
/// joining, never creating.
/// </para>
/// <para>
/// <b>A bounded compromise worth naming.</b> <c>GuideQueryFilter</c> publishes no
/// translation-group predicate and this module does not reach around the repository into
/// persistence, so the group is found by asking for the source guide's own category and country
/// — which a translation copies from its source — and matching the group identifier over that
/// bounded page. It is the same shape of compromise <c>GuideDirectoryService</c> documents for
/// its per-guide loop, and it leaves exactly one call site to improve when the filter grows a
/// <c>TranslationGroupId</c> field.
/// </para>
/// </summary>
public sealed class ListGuideTranslationsQueryHandler
    : IQueryHandler<ListGuideTranslationsQuery, IReadOnlyList<GuideCardResponse>>
{
    private readonly IGuideRepository _guides;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideAuthorNames _authorNames;

    public ListGuideTranslationsQueryHandler(
        IGuideRepository guides, GuideAuthorizationService authorization,
        GuideAuthorNames authorNames)
    {
        _guides = guides;
        _authorization = authorization;
        _authorNames = authorNames;
    }

    public async Task<Result<IReadOnlyList<GuideCardResponse>>> Handle(
        ListGuideTranslationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var source = await _guides.GetForReadAsync(
            GuideId.From(query.GuideId), cancellationToken);

        if (source is null || !_authorization.CanSee(source))
        {
            return Result.Failure<IReadOnlyList<GuideCardResponse>>(GuideErrors.NotFound);
        }

        var filter = new GuideQueryFilter
        {
            Statuses = [GuideStatus.Published],
            CategoryKey = source.CategoryKey,
            CountryCode = source.Location.CountryCode,
            ViewerId = _authorization.CallerId,
            ViewerIsStaff = _authorization.IsStaff,
            Sort = GuideSortOrder.Newest,
            Limit = GuideLimits.MaxPageSize
        };

        var page = await _guides.QueryAsync(
            filter, cursor: null, GuideLimits.MaxPageSize, cancellationToken);

        var translations = page.Items
            .Where(candidate =>
                candidate.TranslationGroupId == source.TranslationGroupId
                && candidate.Id != source.Id)
            .OrderBy(candidate => candidate.Language.Code, StringComparer.Ordinal)
            .ToList();

        return Result.Success(await _authorNames.ToCardsAsync(translations, cancellationToken));
    }
}
