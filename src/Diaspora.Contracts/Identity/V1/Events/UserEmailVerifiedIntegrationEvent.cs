using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Identity.V1.Events;

/// <summary>
/// A member proved they own the email address on their account.
/// <para>
/// <b>Published from 20 Oct 2026.</b> Email verification went live on 5 Oct 2026 and announced
/// itself to nobody: Identity set <c>EmailVerifiedAt</c> and raised only a profile-updated
/// domain event, so Trust's checklist showed the Email row as not done for a member who had
/// done it, and <c>EmailVerificationHandler</c> — built to mirror exactly this, with no document
/// and automatic approval — had nothing to trigger it. The phone had the same gap; see
/// <see cref="UserPhoneVerifiedIntegrationEvent"/>.
/// </para>
/// <para>
/// Delivery is at least once and an operator can publish it again for an account verified before
/// this existed, so consumers must be idempotent.
/// </para>
/// </summary>
public sealed record UserEmailVerifiedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    /// <summary>The address that was proved. Present so a consumer can display it, never to re-verify it.</summary>
    public required string EmailAddress { get; init; }

    public required DateTimeOffset VerifiedAt { get; init; }
}
