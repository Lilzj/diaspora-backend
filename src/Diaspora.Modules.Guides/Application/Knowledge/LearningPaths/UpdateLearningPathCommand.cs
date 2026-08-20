using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.LearningPaths;

/// <summary>Rewrites a learning path's own copy. The key and the country never change.</summary>
[Authorize(Permission = KnowledgePermissions.ManagePaths)]
[Auditable]
public sealed record UpdateLearningPathCommand : ICommand<LearningPathResponse>
{
    public required string CountryCode { get; init; }

    public required string Key { get; init; }

    public required UpdateLearningPathRequest Request { get; init; }
}
