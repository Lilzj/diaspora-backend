namespace Diaspora.Modules.Creator.Domain.Policies;

/// <summary>
/// The module's bounds, in one place.
/// <para>
/// Every one of these is enforced on the server. A limit the client also knows is a courtesy; a
/// limit only the client knows is not a limit.
/// </para>
/// </summary>
public static class CreatorLimits
{
    // ── Profile ─────────────────────────────────────────────────────────────────────────────

    public const int MaxBioLength = 2000;
    public const int MaxCategories = 8;
    public const int MaxExpertise = 12;
    public const int MaxLanguages = 8;
    public const int MaxTagLength = 64;

    // ── Verification ────────────────────────────────────────────────────────────────────────

    public const int MaxEvidenceItems = 12;
    public const int MaxNoteLength = 4000;
    public const int MaxInformationRequests = 10;

    /// <summary>
    /// An approval lasts a year. Long enough that renewal is not an annoyance, short enough that a
    /// creator whose standing changed is re-examined rather than verified for ever.
    /// </summary>
    public const int ApprovalValidDays = 365;

    // ── Grants ──────────────────────────────────────────────────────────────────────────────

    public const int MaxFormFields = 40;
    public const int MaxAnswerLength = 8000;
    public const int MaxDeclarations = 10;
    public const int MaxAttachments = 10;

    /// <summary>Inside this many days of the deadline, a grant reads as ClosingSoon.</summary>
    public const int GrantClosingSoonDays = 7;

    // ── Academy ─────────────────────────────────────────────────────────────────────────────

    public const int MaxLessonBlocks = 200;
    public const int MaxBlockTextLength = 4000;
    public const int MaxListItems = 40;
    public const int MaxQuizQuestions = 40;
    public const int MaxQuizOptions = 8;

    /// <summary>How long a failed quiz makes the member wait. Enough to reread, not enough to sting.</summary>
    public const int QuizRetryCooldownMinutes = 10;

    // ── Referrals ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Length of the shareable code. Eight characters from an unambiguous alphabet is 2.8×10^11
    /// codes — enough that guessing one is not a strategy, short enough to read down a phone.
    /// </summary>
    public const int ReferralCodeLength = 8;

    /// <summary>A referral that has not qualified inside this window expires.</summary>
    public const int ReferralQualificationDays = 90;

    // ── Paging ──────────────────────────────────────────────────────────────────────────────

    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int DefaultLeaderboardSize = 20;
    public const int MaxLeaderboardSize = 100;

    /// <summary>Clamps a caller's requested page size into the module's bounds.</summary>
    public static int PageSize(int? requested) =>
        requested is null or <= 0
            ? DefaultPageSize
            : Math.Min(requested.Value, MaxPageSize);

    /// <summary>Clamps a leaderboard's requested size.</summary>
    public static int LeaderboardSize(int? requested) =>
        requested is null or <= 0
            ? DefaultLeaderboardSize
            : Math.Min(requested.Value, MaxLeaderboardSize);
}
