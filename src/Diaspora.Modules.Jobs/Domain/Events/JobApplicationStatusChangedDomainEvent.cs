using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised on every move through the hiring workflow.
/// <para>
/// One event carrying both ends, from which the relay derives the brief's separate
/// <c>CandidateShortlisted</c> and <c>ApplicationWithdrawn</c> contracts. The aggregate does not
/// know those contracts exist, which is what lets them change without touching it.
/// </para>
/// <para>
/// <b><see cref="IsFirstEmployerResponse"/> is the field the employer-reputation figure is built
/// from.</b> It marks the transition out of <c>Submitted</c> — the moment a real person finally
/// looked — and it is set exactly once per application, so a running average of response times
/// can be maintained without storing every interval.
/// </para>
/// </summary>
public sealed record JobApplicationStatusChangedDomainEvent : DomainEvent
{
    public required JobApplicationId ApplicationId { get; init; }

    public required JobPostingId PostingId { get; init; }

    public required EmployerProfileId EmployerId { get; init; }

    public required CandidateProfileId CandidateId { get; init; }

    public required ApplicationStatus PreviousStatus { get; init; }

    public required ApplicationStatus CurrentStatus { get; init; }

    /// <summary>Who moved it. Null when a sweep or a cascade did.</summary>
    public UserId? ChangedBy { get; init; }

    /// <summary>The reason, when the employer gave one the candidate may see.</summary>
    public string? Reason { get; init; }

    /// <summary>Whether this is the first time the employer engaged. Feeds the response metric.</summary>
    public required bool IsFirstEmployerResponse { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
