namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// One interview round.
/// <para>
/// <see cref="TimeZoneId"/> travels beside the instant so a client can render both clocks —
/// "14:00 your time, 16:00 in Berlin". A candidate three time zones away given only an instant
/// has to do the arithmetic themselves, and the cost of getting it wrong is the job.
/// </para>
/// <para>
/// <see cref="Feedback"/> is the panel's and is present for the employer only. The candidate's
/// view carries <see cref="CandidateFeedback"/>, which is what the employer chose to share and is
/// usually empty.
/// </para>
/// </summary>
public sealed record InterviewResponse
{
    public required Guid InterviewId { get; init; }

    public required Guid ApplicationId { get; init; }

    public required Guid JobId { get; init; }

    public required string JobTitle { get; init; }

    public required string EmployerName { get; init; }

    public required int RoundNumber { get; init; }

    public string? Title { get; init; }

    /// <summary><c>Phone</c>, <c>Video</c>, <c>OnSite</c>, <c>Panel</c>, <c>Technical</c>, <c>Assessment</c>.</summary>
    public required string Mode { get; init; }

    public required DateTimeOffset ScheduledAt { get; init; }

    public required int DurationMinutes { get; init; }

    /// <summary>The employer's IANA wall clock. Render both.</summary>
    public string? TimeZoneId { get; init; }

    public string? LocationAddress { get; init; }

    public string? JoiningUrl { get; init; }

    public string? Instructions { get; init; }

    public required IReadOnlyList<InterviewParticipantResponse> Participants { get; init; }

    public required string Outcome { get; init; }

    public required bool IsCancelled { get; init; }

    public string? CancellationReason { get; init; }

    /// <summary>Surfaced honestly. An employer who has moved it four times has said something.</summary>
    public required int RescheduleCount { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>The panel's notes. Employer view only.</summary>
    public string? Feedback { get; init; }

    /// <summary>What the employer chose to share with the candidate.</summary>
    public string? CandidateFeedback { get; init; }

    public required int Revision { get; init; }
}
