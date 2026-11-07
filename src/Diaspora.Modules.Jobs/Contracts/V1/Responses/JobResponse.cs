namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// One job posting in full — the detail screen.
/// <para>
/// <see cref="Revision"/> is on the response because the client sends it back on the next save.
/// Omitting it would make optimistic concurrency unusable from the outside, which is the same as
/// not having it.
/// </para>
/// <para>
/// The viewer-relative fields at the bottom (<see cref="HasApplied"/>, <see cref="IsSaved"/>,
/// <see cref="CanApply"/>) are what let a client render the right call to action without a second
/// round trip and without deciding the rules itself.
/// </para>
/// </summary>
public sealed record JobResponse
{
    public required Guid JobId { get; init; }

    public required string Slug { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    public required string Description { get; init; }

    // ── Employer ────────────────────────────────────────────────────────────────────────────

    public required JobEmployerSummaryResponse Employer { get; init; }

    // ── Classification ──────────────────────────────────────────────────────────────────────

    public required string CategoryKey { get; init; }

    public required string JobType { get; init; }

    public required string ExperienceLevel { get; init; }

    public required string EducationLevel { get; init; }

    public required IReadOnlyList<string> Tags { get; init; }

    public required IReadOnlyList<JobSkillResponse> Skills { get; init; }

    public required IReadOnlyList<JobDetailLineResponse> Responsibilities { get; init; }

    public required IReadOnlyList<JobDetailLineResponse> Requirements { get; init; }

    public required IReadOnlyList<JobDetailLineResponse> Benefits { get; init; }

    // ── Where and how ───────────────────────────────────────────────────────────────────────

    public required JobLocationResponse Location { get; init; }

    public required JobWorkArrangementResponse WorkArrangement { get; init; }

    public required JobSalaryResponse Salary { get; init; }

    // ── Lifecycle ───────────────────────────────────────────────────────────────────────────

    public required string Status { get; init; }

    public required string Visibility { get; init; }

    public required bool IsFeatured { get; init; }

    public required bool IsVerified { get; init; }

    public required int VacancyCount { get; init; }

    public required int RemainingVacancies { get; init; }

    public required bool RequiresResume { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public DateTimeOffset? ClosingAt { get; init; }

    /// <summary>"Simple" (short one-off work, answered with "I'm interested") or "Standard".</summary>
    public string PostingMode { get; init; } = "Standard";

    /// <summary>The day the work starts, for a job with a schedule.</summary>
    public DateOnly? StartsOn { get; init; }

    /// <summary>How long the work lasts, in <see cref="LengthUnit"/>.</summary>
    public int? LengthAmount { get; init; }

    /// <summary>"Hours" or "Days".</summary>
    public string? LengthUnit { get; init; }

    /// <summary>Why the employer closed it. Applicants are owed an explanation.</summary>
    public string? ClosureReason { get; init; }

    // ── References and questions ────────────────────────────────────────────────────────────

    public required IReadOnlyList<JobMediaReferenceResponse> Media { get; init; }

    public required IReadOnlyList<JobProviderReferenceResponse> Providers { get; init; }

    public required IReadOnlyList<JobGuideReferenceResponse> Guides { get; init; }

    public required IReadOnlyList<JobScreeningQuestionResponse> ScreeningQuestions { get; init; }

    // ── Counters ────────────────────────────────────────────────────────────────────────────

    public required JobEngagementResponse Engagement { get; init; }

    // ── Concurrency ─────────────────────────────────────────────────────────────────────────

    /// <summary>Send this back on the next save. Omitting it opts into last-writer-wins.</summary>
    public required int Revision { get; init; }

    // ── Viewer-relative ─────────────────────────────────────────────────────────────────────

    /// <summary>Whether this viewer has already applied. Null when anonymous.</summary>
    public bool? HasApplied { get; init; }

    public bool? IsSaved { get; init; }

    /// <summary>
    /// Whether an application would be accepted right now. Accounts for the clock, the vacancy
    /// count and the status, so the client does not have to reimplement the rule.
    /// </summary>
    public required bool CanApply { get; init; }

    /// <summary>
    /// Whether this viewer may manage the job through the owner's routes: edit, publish, close,
    /// remove, decide applicants. Only the employer's team. Platform staff are not included since
    /// 7 Nov 2026, when the owner's routes stopped letting them through; see
    /// <see cref="CanModerate"/>.
    /// </summary>
    public required bool CanManage { get; init; }

    /// <summary>
    /// Whether this viewer is platform staff, who act on the job only through moderation and never
    /// through the owner's routes. A client draws its staff view from this, not from
    /// <see cref="CanManage"/>.
    /// </summary>
    public bool CanModerate { get; init; }

    /// <summary>
    /// Whether this viewer belongs to the employer's team. Staff who are not on it moderate the
    /// job but are not its owner: the job is not "theirs", and they may apply to it like anyone
    /// else (6 Nov 2026 — the page told an Admin "You manage this job" and hid "I'm interested",
    /// while the card offered it).
    /// </summary>
    public required bool IsOnEmployerTeam { get; init; }
}
