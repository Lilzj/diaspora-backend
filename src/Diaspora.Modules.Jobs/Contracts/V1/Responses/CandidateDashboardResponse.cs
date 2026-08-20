namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// What a job seeker sees when they open the platform.
/// <para>
/// The order is deliberate and it is the candidate's, not the platform's: outstanding offers
/// first because they expire, then interviews because they are attended, then applications
/// waiting, then saved jobs closing soon. Recommendations come last — a surface that led with
/// suggestions would be optimising for engagement over the thing the member actually came to do.
/// </para>
/// </summary>
public sealed record CandidateDashboardResponse
{
    public Guid? CandidateId { get; init; }

    /// <summary>Null when the member has not created a profile yet. The client prompts.</summary>
    public int? ProfileCompletenessPercent { get; init; }

    // ── Needs an answer ─────────────────────────────────────────────────────────────────────

    /// <summary>Offers waiting on the candidate. These expire, so they lead.</summary>
    public required IReadOnlyList<JobOfferResponse> OutstandingOffers { get; init; }

    public required IReadOnlyList<InterviewResponse> UpcomingInterviews { get; init; }

    // ── In progress ─────────────────────────────────────────────────────────────────────────

    public required int ActiveApplicationCount { get; init; }

    public required int TotalApplicationCount { get; init; }

    /// <summary>Applications the employer has not opened. Honest, and often the real story.</summary>
    public required int UnviewedApplicationCount { get; init; }

    public required IReadOnlyDictionary<string, int> ApplicationsByStatus { get; init; }

    public required IReadOnlyList<ApplicationCardResponse> RecentApplications { get; init; }

    // ── Saved and suggested ─────────────────────────────────────────────────────────────────

    public required int SavedJobCount { get; init; }

    /// <summary>Saved jobs whose closing date is near. The nudge that actually helps.</summary>
    public required IReadOnlyList<JobCardResponse> SavedClosingSoon { get; init; }

    public required int ActiveAlertCount { get; init; }

    /// <summary>Each carries the reason it was suggested. Never unexplained.</summary>
    public required IReadOnlyList<JobCardResponse> Recommended { get; init; }
}
