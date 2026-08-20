using Diaspora.Modules.Jobs.Domain.Enums;

namespace Diaspora.Modules.Jobs.Application.Services;

/// <summary>
/// The one place a domain enum becomes a wire string, and back.
/// <para>
/// <b>Centralised because these strings are frozen contract.</b> Clients switch on them,
/// published integration events carry them, and search documents are keyed by them. Calling
/// <c>ToString()</c> at forty sites would work until somebody renamed an enum member — at which
/// point every consumer breaks silently and no compiler notices. Routing every conversion
/// through here makes a rename a compile error in exactly one file.
/// </para>
/// <para>
/// Parsing is case-insensitive and returns null rather than throwing: an unknown value from a
/// client is an ordinary event — an old app version, a hand-written query string — and answering
/// it by ignoring the filter is far better than answering a browse screen with a 400.
/// </para>
/// </summary>
public static class JobWireNames
{
    public static string ToWire(JobStatus value) => value.ToString();

    public static string ToWire(JobType value) => value.ToString();

    public static string ToWire(WorkplaceType value) => value.ToString();

    public static string ToWire(ExperienceLevel value) => value.ToString();

    public static string ToWire(EducationLevel value) => value.ToString();

    public static string ToWire(ApplicationStatus value) => value.ToString();

    public static string ToWire(InterviewMode value) => value.ToString();

    public static string ToWire(InterviewOutcome value) => value.ToString();

    public static string ToWire(OfferStatus value) => value.ToString();

    public static string ToWire(SalaryPeriod value) => value.ToString();

    public static string ToWire(JobVerificationState value) => value.ToString();

    public static string ToWire(JobModerationState value) => value.ToString();

    public static string ToWire(JobVisibility value) => value.ToString();

    public static string ToWire(EmployerOrganizationType value) => value.ToString();

    public static string ToWire(EmployerStatus value) => value.ToString();

    public static string ToWire(EmployerMemberRole value) => value.ToString();

    public static string ToWire(CompanySize value) => value.ToString();

    public static string ToWire(CandidateVisibility value) => value.ToString();

    public static string ToWire(RemotePreference value) => value.ToString();

    public static string ToWire(WorkAuthorizationKind value) => value.ToString();

    public static string ToWire(LanguageProficiency value) => value.ToString();

    public static string ToWire(SkillProficiency value) => value.ToString();

    public static string ToWire(JobMediaRole value) => value.ToString();

    public static string ToWire(ApplicationAttachmentKind value) => value.ToString();

    public static string ToWire(HiringStageKind value) => value.ToString();

    public static string ToWire(JobAlertFrequency value) => value.ToString();

    public static string ToWire(JobDetailKind value) => value.ToString();

    public static string ToWire(ApplicationEventKind value) => value.ToString();

    public static string ToWire(JobSortOrder value) => value.ToString();

    // ── Parsing ─────────────────────────────────────────────────────────────────────────────

    public static JobStatus? ParseStatus(string? value) => Parse<JobStatus>(value);

    public static JobType? ParseJobType(string? value) => Parse<JobType>(value);

    public static WorkplaceType? ParseWorkplaceType(string? value) => Parse<WorkplaceType>(value);

    public static ExperienceLevel? ParseExperienceLevel(string? value) =>
        Parse<ExperienceLevel>(value);

    public static EducationLevel? ParseEducationLevel(string? value) =>
        Parse<EducationLevel>(value);

    public static ApplicationStatus? ParseApplicationStatus(string? value) =>
        Parse<ApplicationStatus>(value);

    public static InterviewMode? ParseInterviewMode(string? value) => Parse<InterviewMode>(value);

    public static InterviewOutcome? ParseInterviewOutcome(string? value) =>
        Parse<InterviewOutcome>(value);

    public static SalaryPeriod? ParseSalaryPeriod(string? value) => Parse<SalaryPeriod>(value);

    public static JobVisibility? ParseVisibility(string? value) => Parse<JobVisibility>(value);

    public static EmployerOrganizationType? ParseOrganizationType(string? value) =>
        Parse<EmployerOrganizationType>(value);

    public static EmployerMemberRole? ParseMemberRole(string? value) =>
        Parse<EmployerMemberRole>(value);

    public static CompanySize? ParseCompanySize(string? value) => Parse<CompanySize>(value);

    public static CandidateVisibility? ParseCandidateVisibility(string? value) =>
        Parse<CandidateVisibility>(value);

    public static RemotePreference? ParseRemotePreference(string? value) =>
        Parse<RemotePreference>(value);

    public static WorkAuthorizationKind? ParseWorkAuthorization(string? value) =>
        Parse<WorkAuthorizationKind>(value);

    public static LanguageProficiency? ParseLanguageProficiency(string? value) =>
        Parse<LanguageProficiency>(value);

    public static SkillProficiency? ParseSkillProficiency(string? value) =>
        Parse<SkillProficiency>(value);

    public static JobMediaRole? ParseMediaRole(string? value) => Parse<JobMediaRole>(value);

    public static ApplicationAttachmentKind? ParseAttachmentKind(string? value) =>
        Parse<ApplicationAttachmentKind>(value);

    public static HiringStageKind? ParseStageKind(string? value) => Parse<HiringStageKind>(value);

    public static JobAlertFrequency? ParseAlertFrequency(string? value) =>
        Parse<JobAlertFrequency>(value);

    public static JobDetailKind? ParseDetailKind(string? value) => Parse<JobDetailKind>(value);

    public static JobSortOrder? ParseSort(string? value) => Parse<JobSortOrder>(value);

    /// <summary>
    /// Parses several wire names, dropping the ones that mean nothing. A client sending one
    /// unrecognised filter value among five should get the other four applied, not an error.
    /// </summary>
    public static IReadOnlyList<TEnum>? ParseMany<TEnum>(IReadOnlyList<string>? values)
        where TEnum : struct, Enum
    {
        if (values is null || values.Count == 0)
        {
            return null;
        }

        var parsed = values
            .Select(Parse<TEnum>)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .Distinct()
            .ToList();

        return parsed.Count == 0 ? null : parsed;
    }

    private static TEnum? Parse<TEnum>(string? value) where TEnum : struct, Enum =>
        !string.IsNullOrWhiteSpace(value)
        && Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed)
            ? parsed
            : null;
}
