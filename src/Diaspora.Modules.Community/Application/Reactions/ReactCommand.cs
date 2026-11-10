using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Reactions;

/// <summary>Adds or withdraws one reaction on any addressable content.</summary>
[Auditable]
[Authorize(Policy = CommunityPolicies.React)]
public sealed record ReactCommand : ICommand<ReactionResponse>
{
    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required string ReactionKey { get; init; }

    /// <summary>False withdraws. One command rather than two, because the client toggles.</summary>
    public required bool Add { get; init; }
}
