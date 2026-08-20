using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Requests;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Moderation;

/// <summary>A moderator's decision about a piece of content.</summary>
[Auditable]
[Transactional]
public sealed record DecideModerationCommand : ICommand<ModerationDecisionResponse>
{
    public required ModerationDecisionRequest Request { get; init; }
}
