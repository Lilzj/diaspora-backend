using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Requests;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Polls;

/// <summary>Votes in a poll.</summary>
[Auditable]
[Transactional]
[Authorize(Policy = CommunityPolicies.Poll)]
public sealed record CastVoteCommand : ICommand<PollResponse>
{
    public required Guid PostId { get; init; }

    public required CastVoteRequest Request { get; init; }
}
