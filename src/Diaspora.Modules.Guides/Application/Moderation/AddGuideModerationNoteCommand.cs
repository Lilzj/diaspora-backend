using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Moderation;

/// <summary>
/// Appends to the guide's editorial audit trail.
/// <para>
/// Append-only, never edited and never pruned: the trail's whole value is that it records what
/// was known and decided at the time, and a trail that can be tidied afterwards records what
/// somebody later wished had been decided. Never shown to readers.
/// </para>
/// </summary>
[Authorize(Permission = GuidePermissions.Moderate)]
[Auditable]
public sealed record AddGuideModerationNoteCommand : ICommand<GuideResponse>
{
    public required Guid GuideId { get; init; }

    public required AddGuideModerationNoteRequest Request { get; init; }
}
