using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.LearningPaths;

/// <summary>
/// Replaces a learning path's whole sequence, in the order given.
/// <para>
/// <b>The command the brief calls "Arrange Learning Path".</b> Whole-list replacement rather than
/// move operations: a drag-and-drop screen already holds the final ordering, and expressing it as a
/// diff would give the client and the server two chances to disagree about the result with no way
/// for either to notice.
/// </para>
/// <para>
/// Allowed on a published path. The alternative — unpublish, edit, republish — takes a country's
/// onboarding offline for as long as the editor takes, and the commonest edit by far is inserting a
/// step somebody discovered was missing.
/// </para>
/// </summary>
[Authorize(Permission = KnowledgePermissions.ManagePaths)]
[Auditable]
public sealed record ArrangeLearningPathCommand : ICommand<LearningPathResponse>
{
    public required string CountryCode { get; init; }

    public required string Key { get; init; }

    public required ArrangeLearningPathRequest Request { get; init; }
}
