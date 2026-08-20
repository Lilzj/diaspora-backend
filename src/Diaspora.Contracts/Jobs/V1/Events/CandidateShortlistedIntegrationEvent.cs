using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Jobs.V1.Events;

/// <summary>
/// A candidate made the shortlist.
/// <para>
/// <b>Its own contract because it is the first genuinely good news in a job hunt</b>, and the
/// one moment where a notification is unambiguously worth sending. It is published alongside the
/// general status change rather than instead of it, so a notification consumer can subscribe to
/// this alone rather than filtering every transition on the platform.
/// </para>
/// </summary>
public sealed record CandidateShortlistedIntegrationEvent : IntegrationEvent
{
    public required Guid ApplicationId { get; init; }

    public required Guid JobId { get; init; }

    public required Guid EmployerId { get; init; }

    public required Guid CandidateId { get; init; }

    /// <summary>
    /// The member, so the notification can be addressed.
    /// <para>
    /// <b>This is the candidate's member identity, and it is the same fact other contracts in
    /// this namespace call <c>CandidateUserId</c>.</b> The two names predate B13.1 and neither
    /// was renamed, because a published property name is a contract. See the Event Contract
    /// Enrichment Guide for the mapping.
    /// </para>
    /// <para>
    /// Events published before B13.1 carry <see cref="Guid.Empty"/> here: the relay had no route
    /// from a shortlisting to the applying member and wrote a placeholder. A consumer must treat
    /// an empty value as absent rather than as a member.
    /// </para>
    /// </summary>
    public required Guid ApplicantUserId { get; init; }

    /// <summary>The role, so the message can name it without a second read.</summary>
    public required string JobTitle { get; init; }

    public required string EmployerName { get; init; }

    public required DateTimeOffset ShortlistedAt { get; init; }

    /// <summary>
    /// The employer's notifiable member — B13.1. The employer's current primary owner.
    /// <para>
    /// The employer shortlisted, so nothing here is addressed to them. Carried so a consumer
    /// building an employer-side pipeline view has the party without a call back into Jobs.
    /// </para>
    /// </summary>
    public Guid? EmployerUserId { get; init; }
}
