using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Jobs.V1.Events;

/// <summary>
/// An application moved through the hiring workflow.
/// <para>
/// The general contract behind every employer-side decision.
/// <see cref="CandidateShortlistedIntegrationEvent"/> is published alongside it for the one
/// transition worth its own subscription; everything else — under review, interview completed,
/// rejected — arrives here.
/// </para>
/// <para>
/// <b><see cref="IsFirstEmployerResponse"/> is the field an employer's public reputation is
/// built from.</b> It marks the moment a real person finally engaged, is true exactly once per
/// application, and is what lets a running average of response times be maintained without
/// storing every interval.
/// </para>
/// </summary>
public sealed record ApplicationStatusChangedIntegrationEvent : IntegrationEvent
{
    public required Guid ApplicationId { get; init; }

    public required Guid JobId { get; init; }

    public required Guid EmployerId { get; init; }

    public required Guid CandidateId { get; init; }

    public required string PreviousStatus { get; init; }

    public required string CurrentStatus { get; init; }

    /// <summary>Null when a sweep or a cascade moved it rather than a person.</summary>
    public Guid? ChangedBy { get; init; }

    /// <summary>The reason, when the employer gave one the candidate may see.</summary>
    public string? Reason { get; init; }

    /// <summary>True exactly once per application: the first time the employer engaged.</summary>
    public required bool IsFirstEmployerResponse { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }

    /// <summary>
    /// The candidate's member identity — B13.1.
    /// <para>
    /// <b><see cref="CandidateId"/> is a profile, not a person a message can be sent to.</b>
    /// Every candidate-facing message this contract produces — reviewed, rejected, interviewed —
    /// needs the member, and the alternative was Communication resolving a candidate profile
    /// through Jobs on every status change on the platform.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the application could not be read.</para>
    /// </summary>
    public Guid? CandidateUserId { get; init; }

    /// <summary>
    /// The employer's notifiable member — B13.1. The employer's current primary owner.
    /// <para>
    /// Carried because a status change is not always the employer's own doing: a sweep can move
    /// an application, and a cascade from a closed posting moves many at once. Present so the
    /// employer side of the conversation is addressable, not because every transition warrants a
    /// message — that is the routing rules' decision, not this contract's.
    /// </para>
    /// </summary>
    public Guid? EmployerUserId { get; init; }
}
