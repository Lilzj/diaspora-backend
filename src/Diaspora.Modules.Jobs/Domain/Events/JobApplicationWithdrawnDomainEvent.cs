using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when a candidate withdraws.
/// <para>
/// Its own event beside the status change, because the consumers differ: a withdrawal frees a
/// slot in the employer's pipeline, must not count against their response-time figure, and is
/// the one exit that says nothing about the candidate's quality. Collapsing it into "rejected"
/// — as products that model this poorly do — would quietly penalise both parties.
/// </para>
/// <para>
/// <see cref="PreviousStatus"/> is how far they had got. Withdrawing after an offer and
/// withdrawing the day after applying are very different facts about a hiring process.
/// </para>
/// </summary>
public sealed record JobApplicationWithdrawnDomainEvent : DomainEvent
{
    public required JobApplicationId ApplicationId { get; init; }

    public required JobPostingId PostingId { get; init; }

    public required EmployerProfileId EmployerId { get; init; }

    public required CandidateProfileId CandidateId { get; init; }

    /// <summary>How far the candidate had got before leaving.</summary>
    public required ApplicationStatus PreviousStatus { get; init; }

    public UserId? WithdrawnBy { get; init; }

    /// <summary>The candidate's stated reason, if they gave one. Always optional.</summary>
    public string? Reason { get; init; }

    public required DateTimeOffset WithdrawnAt { get; init; }
}
