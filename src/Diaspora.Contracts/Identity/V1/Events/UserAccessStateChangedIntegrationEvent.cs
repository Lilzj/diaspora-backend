using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Identity.V1.Events;

/// <summary>
/// Published on every movement along the progressive access ladder. This is the event
/// downstream modules should react to when deciding what a member may do.
/// </summary>
public sealed record UserAccessStateChangedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public required string FromState { get; init; }

    public required string ToState { get; init; }

    public required string Reason { get; init; }
}

/// <summary>
/// A member proved possession of their phone number.
/// <para>
/// <b>Why this exists when the access-state change already carried it.</b> Verifying a phone is
/// what moves a member off <c>PhoneVerificationPending</c>, so a consumer could infer it from
/// <see cref="UserAccessStateChangedIntegrationEvent.FromState"/> — and one did. That inference
/// holds only while the ladder has a phone rung on it. Moving verification to email takes the
/// rung away, and attaching a phone later, outside registration, never touches the ladder at
/// all: the member is already past it. Both of those turn a working consumer into a silent one.
/// </para>
/// <para>
/// So the fact is published as itself. Whatever the ladder looks like, and wherever in the
/// product the phone is proved, this says the same thing.
/// </para>
/// </summary>
public sealed record UserPhoneVerifiedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    /// <summary>E.164. Present so a consumer can display it, never to re-verify it.</summary>
    public required string PhoneNumber { get; init; }

    public required DateTimeOffset VerifiedAt { get; init; }
}
