using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;
using Diaspora.Modules.Recommendations.Infrastructure.Explainability;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Recommendations.Application.Explainability;

/// <summary>
/// Returns the reason catalogue.
/// <para>
/// Reads from the same table the reason builder renders from, so a client's translation bundle can
/// never be keyed on a code the server does not produce or miss one it does.
/// </para>
/// </summary>
public sealed class GetRecommendationReasonsQueryHandler
    : IQueryHandler<GetRecommendationReasonsQuery, IReadOnlyList<RecommendationReasonCatalogResponse>>
{
    public Task<Result<IReadOnlyList<RecommendationReasonCatalogResponse>>> Handle(
        GetRecommendationReasonsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entries = RecommendationReasonCatalog.KnownCodes
            .Select(code => new RecommendationReasonCatalogResponse
            {
                Code = code.ToString(),
                ResourceKey = RecommendationReasonCatalog.ResourceKeyFor(code),

                // The raw template, placeholder and all: a client building a translation table needs
                // to know which reasons take a detail, and a rendered example would not tell it.
                Template = RecommendationReasonCatalog.TemplateFor(code)
            })
            .OrderBy(entry => entry.Code, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult(
            Result.Success<IReadOnlyList<RecommendationReasonCatalogResponse>>(entries));
    }
}
