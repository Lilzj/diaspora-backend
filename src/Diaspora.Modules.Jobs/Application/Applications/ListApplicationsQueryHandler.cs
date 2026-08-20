using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Modules.Jobs.Domain.Candidates;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Applications;

/// <summary>
/// Lists applications from either end.
/// <para>
/// The posting titles, employer names and candidate headlines are each resolved once for the
/// whole page. A twenty-row applicant list that looked each up per row would be sixty round
/// trips to render one screen.
/// </para>
/// </summary>
public sealed class ListApplicationsQueryHandler
    : IQueryHandler<ListApplicationsQuery, CursorPagedResult<ApplicationCardResponse>>
{
    private readonly IJobApplicationRepository _applications;
    private readonly IJobPostingRepository _postings;
    private readonly IEmployerProfileRepository _employers;
    private readonly ICandidateProfileRepository _candidates;
    private readonly JobAuthorizationService _authorization;
    private readonly IClock _clock;

    public ListApplicationsQueryHandler(
        IJobApplicationRepository applications,
        IJobPostingRepository postings,
        IEmployerProfileRepository employers,
        ICandidateProfileRepository candidates,
        JobAuthorizationService authorization,
        IClock clock)
    {
        _applications = applications;
        _postings = postings;
        _employers = employers;
        _candidates = candidates;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<CursorPagedResult<ApplicationCardResponse>>> Handle(
        ListApplicationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CursorPagedResult<ApplicationCardResponse>>(caller.Error);
        }

        // Which end of the query this is. Naming an employer requires membership of it;
        // naming nothing scopes to the caller's own applications.
        var isEmployerView = query.EmployerId is not null;

        if (isEmployerView)
        {
            var employer = await _authorization.RequireEmployerAsync(
                EmployerProfileId.From(query.EmployerId!.Value),
                EmployerMemberRole.Viewer,
                cancellationToken);

            if (employer.IsFailure)
            {
                return Result.Failure<CursorPagedResult<ApplicationCardResponse>>(employer.Error);
            }
        }

        var page = await _applications.QueryAsync(
            BuildFilter(query, caller.Value, isEmployerView),
            query.Cursor,
            query.Limit,
            cancellationToken);

        if (page.Items.Count == 0)
        {
            return Result.Success(CursorPagedResult<ApplicationCardResponse>.Empty());
        }

        var cards = await BuildCardsAsync(page.Items, isEmployerView, cancellationToken);

        return Result.Success(
            CursorPagedResult<ApplicationCardResponse>.Create(
                cards, page.NextCursor, page.HasMore));
    }

    private static ApplicationQueryFilter BuildFilter(
        ListApplicationsQuery query, SharedKernel.Identity.UserId caller, bool isEmployerView) =>
        new()
        {
            EmployerId = isEmployerView
                ? EmployerProfileId.From(query.EmployerId!.Value)
                : null,
            ApplicantUserId = isEmployerView ? null : caller,
            PostingId = query.JobId is { } jobId ? JobPostingId.From(jobId) : null,
            Statuses = JobWireNames.ParseMany<ApplicationStatus>(query.Statuses),
            ActiveOnly = query.ActiveOnly,
            StageKey = query.StageKey,
            UnviewedOnly = query.UnviewedOnly,
            OldestFirst = query.OldestFirst
        };

    private async Task<IReadOnlyList<ApplicationCardResponse>> BuildCardsAsync(
        IReadOnlyList<JobApplication> applications,
        bool isEmployerView,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        var postings = (await _postings.GetManyAsync(
                [.. applications.Select(application => application.PostingId).Distinct()],
                cancellationToken))
            .ToDictionary(posting => posting.Id.Value);

        var employers = (await _employers.GetManyAsync(
                [.. applications.Select(application => application.EmployerId).Distinct()],
                cancellationToken))
            .ToDictionary(employer => employer.Id.Value);

        // Only the employer's list shows who applied. On a candidate's own tracker it would be
        // telling them their own headline.
        var candidates = isEmployerView
            ? (await _candidates.GetManyAsync(
                    [.. applications.Select(application => application.CandidateId).Distinct()],
                    cancellationToken))
                .ToDictionary(candidate => candidate.Id.Value)
            : [];

        return [.. applications.Select(application =>
        {
            var posting = postings.GetValueOrDefault(application.PostingId.Value);
            var employer = employers.GetValueOrDefault(application.EmployerId.Value);

            return ApplicationMapper.ToCard(
                application,
                posting?.Title.Value ?? string.Empty,
                posting?.Slug.Value ?? string.Empty,
                employer?.Identity.DisplayName ?? string.Empty,
                now,
                employer?.Branding.LogoMediaId,
                candidates.GetValueOrDefault(application.CandidateId.Value)?.Headline.Headline);
        })];
    }
}
