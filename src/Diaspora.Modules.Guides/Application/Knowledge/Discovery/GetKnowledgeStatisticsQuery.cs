using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.Discovery;

/// <summary>
/// What the platform knows about one country, counted, and what people did with it.
/// <para>
/// Public. The knowledge counts are already visible on the country page and hiding the usage figures
/// behind a permission would make the number a client renders on a tile ("142 people looked at this
/// country") require an authenticated call — for aggregate totals with no identity behind them
/// anywhere. The <em>actionable</em> analytics, the unanswered searches, are gated separately.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record GetKnowledgeStatisticsQuery : IQuery<KnowledgeStatisticsResponse>
{
    public required string CountryCode { get; init; }
}
