using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when an interview outcome is recorded.
/// <para>
/// <b>Carries no feedback text, deliberately.</b> An interviewer's notes are internal, and
/// putting them on the message bus would spread a private judgement about a named person into
/// outbox rows and consumer logs. The outcome alone is what downstream needs — the funnel
/// counts it, the pipeline moves on it, and the candidate is told through a status change.
/// </para>
/// </summary>
public sealed record InterviewOutcomeRecordedDomainEvent : DomainEvent
{
    public required InterviewId InterviewId { get; init; }

    public required JobApplicationId ApplicationId { get; init; }

    public required JobPostingId PostingId { get; init; }

    public required InterviewOutcome Outcome { get; init; }

    public required int RoundNumber { get; init; }

    public UserId? RecordedBy { get; init; }

    public required DateTimeOffset RecordedAt { get; init; }
}
