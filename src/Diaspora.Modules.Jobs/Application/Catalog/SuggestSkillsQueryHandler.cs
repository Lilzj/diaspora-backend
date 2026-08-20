using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Catalog;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Catalog;

/// <summary>Suggests skills.</summary>
public sealed class SuggestSkillsQueryHandler
    : IQueryHandler<SuggestSkillsQuery, IReadOnlyList<JobSkillCatalogResponse>>
{
    private readonly IJobSkillRepository _skills;

    public SuggestSkillsQueryHandler(IJobSkillRepository skills) => _skills = skills;

    public async Task<Result<IReadOnlyList<JobSkillCatalogResponse>>> Handle(
        SuggestSkillsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // No term means "what do people ask for" rather than an empty list — which is what a
        // freshly opened skill picker should show.
        var skills = string.IsNullOrWhiteSpace(query.Term)
            ? await _skills.GetPopularAsync(query.CategoryKey, query.Limit, cancellationToken)
            : await _skills.SuggestAsync(query.Term, query.Limit, cancellationToken);

        return Result.Success<IReadOnlyList<JobSkillCatalogResponse>>(
        [
            .. skills.Select(skill => new JobSkillCatalogResponse
            {
                Key = skill.Key,
                Name = skill.Name,
                CategoryKey = skill.CategoryKey,
                UsageCount = skill.UsageCount
            })
        ]);
    }
}
