using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.LearningPaths;

/// <summary>
/// Every onboarding route for a country, without their steps.
/// <para>
/// Unpaged: a country with more than a handful of onboarding routes has a navigation problem that
/// pagination would hide rather than solve.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record ListLearningPathsQuery
    : IQuery<IReadOnlyList<LearningPathSummaryResponse>>
{
    public required string CountryCode { get; init; }

    /// <summary>
    /// Drafts and archived paths too. Honoured only for a curator — a reader asking for them gets
    /// the published ones rather than an error.
    /// </summary>
    public bool IncludeUnpublished { get; init; }
}
