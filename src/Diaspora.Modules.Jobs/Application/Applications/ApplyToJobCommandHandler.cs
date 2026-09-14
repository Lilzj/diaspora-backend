using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Modules.Jobs.Domain.Candidates;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Pipelines;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.Resumes;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.Modules.Jobs.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Jobs.Application.Applications;

/// <summary>
/// Submits an application.
/// <para>
/// The order of the checks is the order that matters to the person applying: is the job still
/// open, have they already applied, are they allowed to, and only then is anything validated.
/// Telling somebody their cover letter is too long before telling them the job closed last week
/// would be the wrong way round.
/// </para>
/// <para>
/// <b>A recruiter for the hiring employer cannot apply to their own posting.</b> Not a
/// hypothetical: an agency recruiter browsing their own board and tapping apply would create an
/// application they can then decide on, which makes the whole audit trail meaningless.
/// </para>
/// <para>
/// <b>Applying needs an account, not a profile — 14 Sep 2026.</b> This refused anyone without a
/// candidate profile, and the app turned the refusal into "Create your profile first": a
/// signed-in member had to write a headline about themselves before an employer would receive
/// anything, although the employer sees them by their account either way. A member's first
/// application now creates their candidate profile, private and empty, and saves it in the same
/// commit as the application — so a refusal further down leaves nothing behind.
/// </para>
/// </summary>
public sealed class ApplyToJobCommandHandler
    : ICommandHandler<ApplyToJobCommand, JobApplicationResponse>
{
    private readonly IJobApplicationRepository _applications;
    private readonly IJobPostingRepository _postings;
    private readonly ICandidateProfileRepository _candidates;
    private readonly IResumeRepository _resumes;
    private readonly IHiringPipelineRepository _pipelines;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly JobMediaClaimer _media;
    private readonly IOptions<JobOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public ApplyToJobCommandHandler(
        IJobApplicationRepository applications,
        IJobPostingRepository postings,
        ICandidateProfileRepository candidates,
        IResumeRepository resumes,
        IHiringPipelineRepository pipelines,
        IUnitOfWork unitOfWork,
        JobAuthorizationService authorization,
        JobMediaClaimer media,
        IOptions<JobOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _applications = applications;
        _postings = postings;
        _candidates = candidates;
        _resumes = resumes;
        _pipelines = pipelines;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _media = media;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<JobApplicationResponse>> Handle(
        ApplyToJobCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<JobApplicationResponse>(caller.Error);
        }

        // Null for a member applying for the first time. Their profile is made below, once every
        // check that could refuse the application has passed.
        var existingCandidate = await _candidates.GetByUserAsync(caller.Value, cancellationToken);

        var posting = await _postings.GetAsync(
            JobPostingId.From(command.JobId), cancellationToken);

        // Visibility first, and before any reason that would confirm the posting exists. A draft
        // or a hidden posting reads as not found on every other route; answering "applications
        // are closed" here would tell a stranger who guessed an identifier that there is
        // something behind it.
        if (posting is null || !await _authorization.CanSeeAsync(posting, cancellationToken))
        {
            return Result.Failure<JobApplicationResponse>(JobErrors.PostingNotFound);
        }

        var now = _clock.UtcNow;

        if (!posting.AcceptsApplications(now))
        {
            // The most specific reason, so the candidate knows whether to wait or move on.
            return Result.Failure<JobApplicationResponse>(
                posting.RemainingVacancies == 0
                    ? JobErrors.NoVacanciesRemaining
                    : posting.HasClosingDatePassed(now)
                        ? JobErrors.PostingExpired
                        : JobErrors.ApplicationsClosed);
        }

        // A recruiter applying to their own employer's posting would create an application they
        // can then decide on. Refused outright.
        if (await _authorization.IsMemberOfAsync(posting.EmployerId, cancellationToken))
        {
            return Result.Failure<JobApplicationResponse>(JobErrors.SelfApplication);
        }

        // Somebody with no candidate profile has never applied for anything, so there is nothing
        // to find.
        if (existingCandidate is not null
            && await _applications.GetByCandidateAndPostingAsync(
                existingCandidate.Id, posting.Id, cancellationToken) is not null)
        {
            return Result.Failure<JobApplicationResponse>(JobErrors.AlreadyApplied);
        }

        var sentToday = await _applications.CountByApplicantSinceAsync(
            caller.Value, now.AddDays(-1), cancellationToken);

        if (sentToday >= _options.Value.MaxApplicationsPerCandidatePerDay)
        {
            return Result.Failure<JobApplicationResponse>(JobErrors.TooManyApplications);
        }

        var resume = await ResolveResumeAsync(command.Request, existingCandidate?.Id, cancellationToken);

        if (resume.IsFailure)
        {
            return Result.Failure<JobApplicationResponse>(resume.Error);
        }

        if (posting.RequiresResume && resume.Value is null)
        {
            return Result.Failure<JobApplicationResponse>(JobErrors.ResumeRequired);
        }

        var answers = BuildAnswers(command.Request, posting);

        if (answers.IsFailure)
        {
            return Result.Failure<JobApplicationResponse>(answers.Error);
        }

        var attachments = BuildAttachments(command.Request);

        if (attachments.IsFailure)
        {
            return Result.Failure<JobApplicationResponse>(attachments.Error);
        }

        var candidate = existingCandidate ?? await ProvisionCandidateAsync(caller.Value, now, cancellationToken);

        if (candidate is null)
        {
            return Result.Failure<JobApplicationResponse>(JobErrors.NotCandidate);
        }

        var application = JobApplication.Submit(
            JobApplicationId.From(_guids.NewSequentialGuid()),
            posting.Id,
            posting.EmployerId,
            candidate.Id,
            candidate.UserId,
            resume.Value?.Id,
            command.Request.CoverLetter,
            answers.Value,
            attachments.Value,
            now,

            // The posting is already loaded above to check it accepts applications, so
            // carrying what the job was about costs nothing. PSR-1 / DEBT-006.
            posting.CategoryKey,
            posting.Location.CountryCode);

        if (application.IsFailure)
        {
            return Result.Failure<JobApplicationResponse>(application.Error);
        }

        await _applications.AddAsync(application.Value, cancellationToken);

        // The posting learns about it: the counter moves and an Open posting becomes
        // ReceivingApplications. Done through the aggregate rather than by touching a column.
        var noted = posting.NoteApplicationReceived(now);

        if (noted.IsFailure)
        {
            return Result.Failure<JobApplicationResponse>(noted.Error);
        }

        await PlaceInPipelineAsync(application.Value, posting, now, cancellationToken);

        resume.Value?.RecordUse();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // After the commit. An unclaimed attachment is swept by Media a day later, silently.
        await ClaimAttachmentsAsync(application.Value, cancellationToken);

        return Result.Success(ApplicationMapper.ToResponse(
            application.Value,
            posting.Title.Value,
            posting.Slug.Value,
            employerName: string.Empty,
            forEmployer: false,
            now,
            resume.Value?.Label));
    }

    /// <summary>
    /// The chosen résumé, or the candidate's default. Null is a valid answer — only a posting
    /// that requires one refuses it. A member with no candidate profile has no résumés, so any
    /// named one is somebody else's.
    /// </summary>
    private async Task<Result<Resume?>> ResolveResumeAsync(
        ApplyToJobRequest request,
        CandidateProfileId? candidateId,
        CancellationToken cancellationToken)
    {
        if (request.ResumeId is not { } requested)
        {
            return candidateId is null
                ? Result.Success<Resume?>(null)
                : Result.Success(await _resumes.GetDefaultAsync(candidateId, cancellationToken));
        }

        var resume = await _resumes.GetAsync(ResumeId.From(requested), cancellationToken);

        if (resume is null)
        {
            return Result.Failure<Resume?>(JobErrors.ResumeNotFound);
        }

        // Ownership, because a résumé identifier is guessable and an application carrying
        // somebody else's document would be a data leak with a plausible-looking cause.
        return candidateId is not null && resume.CandidateId == candidateId
            ? Result.Success<Resume?>(resume)
            : Result.Failure<Resume?>(JobErrors.ResumeNotOwned);
    }

    /// <summary>
    /// A first-time applicant's candidate profile: private, with nothing written on it.
    /// <para>
    /// Added to the unit of work and not saved here. It commits with the application or not at
    /// all, so an application refused after this point cannot leave an empty profile behind.
    /// Null only if the aggregate refuses to create one, which with no country to validate it
    /// has no reason to do.
    /// </para>
    /// </summary>
    private async Task<CandidateProfile?> ProvisionCandidateAsync(
        SharedKernel.Identity.UserId userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var created = CandidateProfile.Create(
            CandidateProfileId.From(_guids.NewSequentialGuid()),
            userId,
            CandidateHeadline.None,
            countryCode: null,
            now);

        if (created.IsFailure)
        {
            return null;
        }

        await _candidates.AddAsync(created.Value, cancellationToken);

        return created.Value;
    }

    /// <summary>
    /// Builds the answers, taking the question text from the posting rather than the request.
    /// A candidate must not be able to choose what they appear to have been asked.
    /// </summary>
    private static Result<IReadOnlyList<ApplicationAnswer>> BuildAnswers(
        ApplyToJobRequest request, JobPosting posting)
    {
        var supplied = request.Answers
            .Where(answer => answer is not null)
            .ToDictionary(answer => answer.QuestionId, answer => answer.Answer);

        var answers = new List<ApplicationAnswer>();
        var order = 0;

        foreach (var question in posting.ScreeningQuestions)
        {
            if (!supplied.TryGetValue(question.QuestionId, out var text)
                || string.IsNullOrWhiteSpace(text))
            {
                if (question.IsRequired)
                {
                    return Result.Failure<IReadOnlyList<ApplicationAnswer>>(
                        JobErrors.AnswerInvalid);
                }

                continue;
            }

            var answer = ApplicationAnswer.Create(
                question.QuestionId, question.Text, text, order);

            if (answer is null)
            {
                return Result.Failure<IReadOnlyList<ApplicationAnswer>>(JobErrors.AnswerInvalid);
            }

            answers.Add(answer);
            order++;
        }

        return Result.Success<IReadOnlyList<ApplicationAnswer>>(answers);
    }

    private static Result<IReadOnlyList<ApplicationAttachment>> BuildAttachments(
        ApplyToJobRequest request)
    {
        var attachments = new List<ApplicationAttachment>();
        var order = 0;

        foreach (var draft in request.Attachments)
        {
            if (draft is null)
            {
                return Result.Failure<IReadOnlyList<ApplicationAttachment>>(
                    JobErrors.AttachmentInvalid);
            }

            var kind = JobWireNames.ParseAttachmentKind(draft.Kind);

            if (kind is null)
            {
                return Result.Failure<IReadOnlyList<ApplicationAttachment>>(
                    JobErrors.AttachmentInvalid);
            }

            var attachment = ApplicationAttachment.Create(
                Guid.Empty, kind.Value, draft.MediaId, draft.FileName, order);

            if (attachment is null)
            {
                return Result.Failure<IReadOnlyList<ApplicationAttachment>>(
                    JobErrors.AttachmentInvalid);
            }

            attachments.Add(attachment);
            order++;
        }

        return Result.Success<IReadOnlyList<ApplicationAttachment>>(attachments);
    }

    /// <summary>
    /// Places the application in the posting's entry stage. A missing pipeline is not fatal —
    /// the application is still valid and the employer can still work it by status.
    /// </summary>
    private async Task PlaceInPipelineAsync(
        JobApplication application,
        JobPosting posting,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var pipeline = await _pipelines.GetForPostingAsync(posting.Id, cancellationToken);

        if (pipeline?.EntryStage is { } entry)
        {
            application.MoveToStage(entry.Key, now, changedBy: null);
        }
    }

    private async Task ClaimAttachmentsAsync(
        JobApplication application, CancellationToken cancellationToken)
    {
        if (application.Attachments.Count == 0)
        {
            return;
        }

        await _media.ClaimManyAsync(
            application.Id.Value,
            [.. application.Attachments.Select(attachment =>
                (attachment.MediaId, JobWireNames.ToWire(attachment.Kind)))],
            cancellationToken);
    }
}
