using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.Suggestions;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Suggestions;

/// <summary>
/// Serves a guide's suggestions.
/// <para>
/// Visibility is the guide's: a suggestion on a draft is only visible to whoever can see the
/// draft, because the suggestion quotes it. An unrecognised status name widens to every status
/// rather than failing — a stale filter chip should not error a list.
/// </para>
/// </summary>
public sealed class ListGuideSuggestionsQueryHandler
    : IQueryHandler<ListGuideSuggestionsQuery, CursorPagedResult<GuideSuggestionResponse>>
{
    private readonly IGuideRepository _guides;
    private readonly IGuideSuggestionRepository _suggestions;
    private readonly GuideAuthorizationService _authorization;

    public ListGuideSuggestionsQueryHandler(
        IGuideRepository guides,
        IGuideSuggestionRepository suggestions,
        GuideAuthorizationService authorization)
    {
        _guides = guides;
        _suggestions = suggestions;
        _authorization = authorization;
    }

    public async Task<Result<CursorPagedResult<GuideSuggestionResponse>>> Handle(
        ListGuideSuggestionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var guideId = GuideId.From(query.GuideId);
        var guide = await _guides.GetForReadAsync(guideId, cancellationToken);

        if (guide is null || !_authorization.CanSee(guide))
        {
            return Result.Failure<CursorPagedResult<GuideSuggestionResponse>>(
                GuideErrors.NotFound);
        }

        var limit = Math.Clamp(
            query.Limit ?? GuideLimits.DefaultPageSize, 1, GuideLimits.MaxPageSize);

        var status =
            Enum.TryParse<GuideSuggestionStatus>(
                query.Status?.Trim(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed)
                ? parsed
                : (GuideSuggestionStatus?)null;

        var page = await _suggestions.ListForGuideAsync(
            guideId, status, query.Cursor, limit, cancellationToken);

        return Result.Success(CursorPagedResult<GuideSuggestionResponse>.Create(
            page.Items.Select(GuideSuggestionMapper.ToResponse).ToList(),
            page.NextCursor,
            page.HasMore));
    }
}
