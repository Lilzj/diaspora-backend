using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Jobs.Application.Mapping;

/// <summary>
/// Turns an application into the shapes the wire carries.
/// <para>
/// <b>The same aggregate renders differently for each party, and the difference is the history.</b>
/// <paramref name="forEmployer"/> decides whether the timeline includes the employer's internal
/// entries — a note reading "strong, but we already have someone" belongs on the record and
/// emphatically not in front of the candidate. Doing the filter here rather than in a client is
/// what makes it impossible to forget.
/// </para>
/// </summary>
public static class ApplicationMapper
{
    public static JobApplicationResponse ToResponse(
        JobApplication application,
        string jobTitle,
        string jobSlug,
        string employerName,
        bool forEmployer,
        DateTimeOffset now,
        string? resumeLabel = null)
    {
        ArgumentNullException.ThrowIfNull(application);

        // The employer sees everything; the candidate sees what was marked visible to them.
        var history = forEmployer
            ? application.History
            : application.CandidateVisibleHistory;

        return new JobApplicationResponse
        {
            ApplicationId = application.Id.Value,
            JobId = application.PostingId.Value,
            JobTitle = jobTitle,
            JobSlug = jobSlug,
            EmployerId = application.EmployerId.Value,
            EmployerName = employerName,
            CandidateId = application.CandidateId.Value,

            Status = JobWireNames.ToWire(application.Status),
            StageKey = application.StageKey,
            CoverLetter = application.CoverLetter,
            ResumeId = application.ResumeId?.Value,
            ResumeLabel = resumeLabel,

            Answers = [.. application.Answers.Select(answer => new ApplicationAnswerResponse
            {
                QuestionId = answer.QuestionId,
                QuestionText = answer.QuestionText,
                Answer = answer.Answer
            })],

            Attachments = [.. application.Attachments.Select(ToAttachment)],

            History = [.. history.Select(entry => new ApplicationHistoryResponse
            {
                EntryId = entry.EntryId,
                Kind = JobWireNames.ToWire(entry.Kind),
                FromStatus = entry.FromStatus is { } from ? JobWireNames.ToWire(from) : null,
                ToStatus = entry.ToStatus is { } to ? JobWireNames.ToWire(to) : null,
                Note = entry.Note,
                OccurredAt = entry.OccurredAt
            })],

            SubmittedAt = application.SubmittedAt,

            // Only the employer learns when they opened it. Showing a candidate the exact
            // moment somebody read their application and then did nothing for three weeks is a
            // product decision nobody has made yet, and the safe default is not to.
            FirstViewedAt = forEmployer ? application.FirstViewedAt : null,

            DecidedAt = application.DecidedAt,
            DecisionReason = application.DecisionReason,
            HoursToFirstResponse = application.TimeToFirstResponse?.TotalHours,
            IsActive = application.IsActive,
            CanWithdraw = application.CanWithdraw,
            Revision = application.Revision
        };
    }

    /// <summary>
    /// The list row.
    /// <para>
    /// <paramref name="candidateHeadline"/> and <paramref name="applicant"/> are supplied on the
    /// employer's applicant list and left null on the candidate's own tracker — where they would be
    /// telling the candidate their own name and headline.
    /// </para>
    /// </summary>
    public static ApplicationCardResponse ToCard(
        JobApplication application,
        string jobTitle,
        string jobSlug,
        string employerName,
        DateTimeOffset now,
        Guid? employerLogoMediaId = null,
        string? candidateHeadline = null,
        MemberProfile? applicant = null)
    {
        ArgumentNullException.ThrowIfNull(application);

        return new ApplicationCardResponse
        {
            ApplicationId = application.Id.Value,
            JobId = application.PostingId.Value,
            JobTitle = jobTitle,
            JobSlug = jobSlug,
            EmployerId = application.EmployerId.Value,
            EmployerName = employerName,
            EmployerLogoMediaId = employerLogoMediaId,
            CandidateId = application.CandidateId.Value,
            CandidateHeadline = string.IsNullOrWhiteSpace(candidateHeadline) ? null : candidateHeadline,
            ApplicantName = applicant?.DisplayName,
            ApplicantIsVerified = applicant?.IsVerified,

            Status = JobWireNames.ToWire(application.Status),
            StageKey = application.StageKey,
            SubmittedAt = application.SubmittedAt,
            LastActivityAt = application.LastActivityAt,
            HasBeenViewed = application.HasBeenViewed,

            // Null once decided: "waiting 40 days" on a closed application is not a wait.
            DaysWaiting = application.DecidedAt is null
                ? (int)Math.Floor((now - application.SubmittedAt).TotalDays)
                : null,

            IsActive = application.IsActive,
            AttachmentCount = application.Attachments.Count
        };
    }

    private static ApplicationAttachmentResponse ToAttachment(ApplicationAttachment attachment) =>
        new()
        {
            AttachmentId = attachment.AttachmentId,
            Kind = JobWireNames.ToWire(attachment.Kind),
            MediaId = attachment.MediaId,
            FileName = attachment.FileName
        };
}
