using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Mapping;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.LearningPaths;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.LearningPaths;

/// <summary>Lists a country's onboarding routes.</summary>
public sealed class ListLearningPathsQueryHandler
    : IQueryHandler<ListLearningPathsQuery, IReadOnlyList<LearningPathSummaryResponse>>
{
    private readonly ILearningPathRepository _paths;
    private readonly KnowledgeAuthorizationService _authorization;

    public ListLearningPathsQueryHandler(
        ILearningPathRepository paths, KnowledgeAuthorizationService authorization)
    {
        _paths = paths;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<LearningPathSummaryResponse>>> Handle(
        ListLearningPathsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // A reader asking for unpublished paths gets the published ones. The flag is a curator's
        // tool, and refusing the listing over it would break a country page for a client bug.
        var publishedOnly = !(query.IncludeUnpublished && _authorization.IsCurator);

        var paths = await _paths.ListByCountryAsync(
            query.CountryCode, publishedOnly, cancellationToken);

        return Result.Success<IReadOnlyList<LearningPathSummaryResponse>>(
            [.. paths.Select(LearningPathMapper.ToSummary)]);
    }
}
