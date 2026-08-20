using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Interviews;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Interviews;

/// <summary>
/// Reads the interview rounds on an application, for whichever side of the table is asking.
/// </summary>
public sealed class ListApplicationInterviewsQueryHandler
    : IQueryHandler<ListApplicationInterviewsQuery, IReadOnlyList<InterviewResponse>>
{
    private readonly IInterviewRepository _interviews;
    private readonly IJobApplicationRepository _applications;
    private readonly IJobPostingRepository _postings;
    private readonly IEmployerProfileRepository _employers;
    private readonly JobAuthorizationService _authorization;

    public ListApplicationInterviewsQueryHandler(
        IInterviewRepository interviews,
        IJobApplicationRepository applications,
        IJobPostingRepository postings,
        IEmployerProfileRepository employers,
        JobAuthorizationService authorization)
    {
        _interviews = interviews;
        _applications = applications;
        _postings = postings;
        _employers = employers;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<InterviewResponse>>> Handle(
        ListApplicationInterviewsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var application = await _applications.GetForReadAsync(
            JobApplicationId.From(query.ApplicationId), cancellationToken);

        if (application is null)
        {
            return Result.Failure<IReadOnlyList<InterviewResponse>>(JobErrors.ApplicationNotFound);
        }

        // ── Who may read this ────────────────────────────────────────────────────────────────
        //
        // The employer's team, and the candidate whose application it is. Nobody else, and
        // deliberately not "anybody who can see the posting": an interview round names a time, a
        // joining link and the people attending.
        //
        // `ApplicationNotFound` rather than a forbidden result for an outsider, because 403 on a
        // guessable identifier confirms that an application exists between two named parties.
        var isEmployer = await _authorization.IsMemberOfAsync(application.EmployerId, cancellationToken);
        var isCandidate = _authorization.RequireOwner(application.ApplicantUserId).IsSuccess;

        if (!isEmployer && !isCandidate)
        {
            return Result.Failure<IReadOnlyList<InterviewResponse>>(JobErrors.ApplicationNotFound);
        }

        var interviews = await _interviews.GetForApplicationAsync(application.Id, cancellationToken);

        if (interviews.Count == 0)
        {
            // An application with no interviews yet is an ordinary state, not an error. The
            // employer dashboard's `upcomingInterviewCount` can legitimately be zero.
            return Result.Success<IReadOnlyList<InterviewResponse>>([]);
        }

        var posting = await _postings.GetForReadAsync(application.PostingId, cancellationToken);
        var employer = await _employers.GetForReadAsync(application.EmployerId, cancellationToken);

        // The title and employer name are denormalised onto the response so a list renders without
        // a second round trip. A deleted posting must not take the interview list down with it —
        // the interview still happened, and the candidate still needs to know when.
        var jobTitle = posting?.Title.Value ?? string.Empty;
        var employerName = employer?.Identity.DisplayName ?? string.Empty;

        return Result.Success<IReadOnlyList<InterviewResponse>>(
            [.. interviews
                .OrderBy(interview => interview.RoundNumber)
                .Select(interview => InterviewMapper.ToResponse(
                    interview, jobTitle, employerName, forEmployer: isEmployer))]);
    }
}
