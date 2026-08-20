using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

namespace Diaspora.Modules.CommunityLife.Application.Moderation;

/// <summary>Hides, suspends or clears an event or an organization. Staff only.</summary>
[Authorize(Permission = CommunityLifePermissions.Moderate)]
[Auditable]
public sealed record ModerateSubjectCommand : ICommand
{
    public required string SubjectType { get; init; }

    public required Guid SubjectId { get; init; }

    public required ModerateSubjectRequest Request { get; init; }
}
