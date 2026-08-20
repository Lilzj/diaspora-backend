namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// What a recruiter sees when they open the platform.
/// <para>
/// <see cref="UnviewedApplicationCount"/> and <see cref="StaleApplicationCount"/> lead
/// deliberately. The most useful thing a hiring dashboard can do is tell an employer how many
/// people are waiting on them — and the second number, applications nobody has answered past the
/// threshold, is the one that feeds their public ghosting rate. Showing it to them before it
/// shows to candidates is the fair order.
/// </para>
/// </summary>
public sealed record EmployerDashboardResponse
{
    public required Guid EmployerId { get; init; }

    public required string EmployerName { get; init; }

    public required bool IsVerified { get; init; }

    // ── Postings ────────────────────────────────────────────────────────────────────────────

    public required int DraftCount { get; init; }

    public required int OpenCount { get; init; }

    public required int ClosedCount { get; init; }

    public required int FilledCount { get; init; }

    // ── People waiting ──────────────────────────────────────────────────────────────────────

    public required int TotalApplicationCount { get; init; }

    public required int ActiveApplicationCount { get; init; }

    /// <summary>Applications nobody at the employer has opened.</summary>
    public required int UnviewedApplicationCount { get; init; }

    /// <summary>Unanswered past the platform's threshold. Feeds the public ghosting rate.</summary>
    public required int StaleApplicationCount { get; init; }

    /// <summary>The whole funnel by status, across every posting.</summary>
    public required IReadOnlyDictionary<string, int> ApplicationsByStatus { get; init; }

    // ── Coming up ───────────────────────────────────────────────────────────────────────────

    public required int UpcomingInterviewCount { get; init; }

    public required int OutstandingOfferCount { get; init; }

    public required EmployerResponsivenessResponse Responsiveness { get; init; }

    /// <summary>The postings most in need of attention, newest first.</summary>
    public required IReadOnlyList<JobCardResponse> RecentJobs { get; init; }
}
