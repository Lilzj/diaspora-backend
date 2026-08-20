using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Analytics;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.SharedKernel.Results;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Analytics;

/// <summary>
/// Serves the author's dashboard.
/// <para>
/// <b>A missing analytics row is a page of zeroes, never a creation.</b> The row is created on
/// the first recorded view, by the command that records it; a dashboard visit that created one
/// would make the read a write, would need a transaction, and would put an author's own visit at
/// the top of their readership.
/// </para>
/// <para>
/// Author or staff, checked against the guide rather than against the analytics row: the row has
/// no owner of its own, and asking the guide is what makes "my guide" mean the same thing here
/// as everywhere else in the module.
/// </para>
/// </summary>
public sealed class GetGuideAnalyticsQueryHandler
    : IQueryHandler<GetGuideAnalyticsQuery, GuideAnalyticsResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IGuideAnalyticsRepository _analytics;
    private readonly GuideAuthorizationService _authorization;

    public GetGuideAnalyticsQueryHandler(
        IGuideRepository guides,
        IGuideAnalyticsRepository analytics,
        GuideAuthorizationService authorization)
    {
        _guides = guides;
        _analytics = analytics;
        _authorization = authorization;
    }

    public async Task<Result<GuideAnalyticsResponse>> Handle(
        GetGuideAnalyticsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var guideId = GuideId.From(query.GuideId);
        var guide = await _guides.GetForReadAsync(guideId, cancellationToken);

        if (guide is null || guide.IsDeleted)
        {
            return Result.Failure<GuideAnalyticsResponse>(GuideErrors.NotFound);
        }

        var author = _authorization.RequireAuthor(guide);

        if (author.IsFailure)
        {
            return Result.Failure<GuideAnalyticsResponse>(author.Error);
        }

        var analytics = await _analytics.GetByGuideAsync(guideId, cancellationToken);

        return Result.Success(analytics is null
            ? GuideAnalyticsMapper.Empty(guide.Id.Value)
            : GuideAnalyticsMapper.ToResponse(analytics));
    }
}
