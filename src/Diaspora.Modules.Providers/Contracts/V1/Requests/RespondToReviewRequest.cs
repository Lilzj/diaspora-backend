namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>
/// The provider's public reply to a review.
/// <para>
/// One reply, not a thread. A public argument between an owner and a customer helps nobody, and
/// a threaded conversation is what Community is for if the platform ever decides it wants one.
/// Sending an empty body withdraws the reply.
/// </para>
/// </summary>
public sealed record RespondToReviewRequest
{
    public string? Response { get; init; }
}
