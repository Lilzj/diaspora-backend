using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.LearningPaths;

/// <summary>
/// One onboarding route, with each step's guide resolved.
/// <para>
/// The screen a newcomer follows. Addressed by country and key, because the pair is what a deep link
/// carries and what makes <c>arriving</c> a usable key for every country at once.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record GetLearningPathQuery : IQuery<LearningPathResponse>
{
    public required string CountryCode { get; init; }

    public required string Key { get; init; }
}
