using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Community.V1.Events;

/// <summary>
/// Community corroboration moved a post across a threshold.
/// <para>
/// The largest ranking change that can happen to a post without anybody editing it, which makes
/// this the event a ranking cache must not ignore. It is also the one a notification module
/// should act on: an author whose safety post has just been confirmed by ten neighbours wants to
/// know.
/// </para>
/// </summary>
public sealed record CommunityPostVerificationChangedIntegrationEvent : IntegrationEvent
{
    public required Guid PostId { get; init; }

    /// <summary><c>unverified</c>, <c>communityVerified</c> or <c>disputed</c>.</summary>
    public required string Previous { get; init; }

    public required string Current { get; init; }

    public required int Confirmations { get; init; }

    public required int VerifiedConfirmations { get; init; }
}
