using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.LearningPaths;

/// <summary>
/// Opens a curated onboarding route for a country.
/// <para>
/// Created as a draft with no steps. Arranging and publishing are separate acts, because a path is
/// written over days and must not be half-visible to newcomers while it is.
/// </para>
/// </summary>
[RequirePermission(KnowledgePermissions.ManagePaths)]
[Auditable]
public sealed record CreateLearningPathCommand : ICommand<LearningPathResponse>
{
    /// <summary>ISO 3166-1 alpha-2, from the route.</summary>
    public required string CountryCode { get; init; }

    public required CreateLearningPathRequest Request { get; init; }
}
