using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;

namespace Diaspora.Modules.Recommendations.Application.Explainability;

/// <summary>
/// Every reason the platform can give.
/// <para>
/// <b>Exists so a client can discover reason codes at runtime rather than at build time.</b> A new
/// ranking strategy can introduce a code at any release; a client that only knew the codes it shipped
/// with would render English on the one line of the card whose whole job is to be trusted, until the
/// next app update reached the member — which in the countries this product serves can be months.
/// </para>
/// </summary>
[Authorize(Permission = RecommendationPermissions.FeedRead)]
public sealed record GetRecommendationReasonsQuery
    : IQuery<IReadOnlyList<RecommendationReasonCatalogResponse>>;
