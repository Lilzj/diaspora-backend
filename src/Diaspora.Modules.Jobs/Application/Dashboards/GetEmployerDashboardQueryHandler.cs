using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Interviews;
using Diaspora.Modules.Jobs.Domain.Offers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Jobs.Application.Dashboards;

/// <summary>
/// Builds the employer dashboard.
/// <para>
/// <b>The two numbers it leads with are the ones an employer would rather not see:</b> how many
/// applications nobody has opened, and how many have gone unanswered past the platform's
/// threshold. The second feeds their public ghosting rate, and showing it to them before it shows
/// to candidates is the fair order.
/// </para>
/// </summary>
public sealed class GetEmployerDashboardQueryHandler
    : IQueryHandler<GetEmployerDashboardQuery, EmployerDashboardResponse>
{
    private const int RecentJobCount = 5;

    private readonly IJobPostingRepository _postings;
    private readonly IJobApplicationRepository _applications;
    private readonly IInterviewRepository _interviews;
    private readonly IJobOfferRepository _offers;
    private readonly JobAuthorizationService _authorization;
    private readonly IOptions<JobOptions> _options;
    private readonly IClock _clock;

    public GetEmployerDashboardQueryHandler(
        IJobPostingRepository postings,
        IJobApplicationRepository applications,
        IInterviewRepository interviews,
        IJobOfferRepository offers,
        JobAuthorizationService authorization,
        IOptions<JobOptions> options,
        IClock clock)
    {
        _postings = postings;
        _applications = applications;
        _interviews = interviews;
        _offers = offers;
        _authorization = authorization;
        _options = options;
        _clock = clock;
    }

    public async Task<Result<EmployerDashboardResponse>> Handle(
        GetEmployerDashboardQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var employerId = await ResolveEmployerIdAsync(query, cancellationToken);

        if (employerId is null)
        {
            return Result.Failure<EmployerDashboardResponse>(JobErrors.EmployerNotFound);
        }

        var employer = await _authorization.RequireEmployerReadAsync(
            employerId, EmployerMemberRole.Viewer, cancellationToken);

        if (employer.IsFailure)
        {
            return Result.Failure<EmployerDashboardResponse>(employer.Error);
        }

        var now = _clock.UtcNow;
        var staleBefore = now.AddDays(-_options.Value.GhostingThresholdDays);

        var byStatus = await _applications.CountByStatusForEmployerAsync(
            employerId, cancellationToken);

        var scope = new ApplicationQueryFilter { EmployerId = employerId };

        var activeCount = await _applications.CountAsync(
            scope with { ActiveOnly = true }, cancellationToken);

        var unviewedCount = await _applications.CountAsync(
            scope with { UnviewedOnly = true, ActiveOnly = true }, cancellationToken);

        var staleCount = await _applications.CountAsync(
            scope with { UnansweredBefore = staleBefore }, cancellationToken);

        var upcomingInterviews = await _interviews.GetUpcomingForEmployerAsync(
            employerId, now, 50, cancellationToken);

        var outstandingOffers = await _offers.GetOutstandingForEmployerAsync(
            employerId, cancellationToken);

        var recent = await _postings.QueryAsync(
            new JobQueryFilter
            {
                EmployerId = employerId,
                ViewerEmployerId = employerId,
                IncludeNonPublic = true,
                Sort = JobSortOrder.Newest
            },
            cursor: null,
            RecentJobCount,
            cancellationToken);

        return Result.Success(new EmployerDashboardResponse
        {
            EmployerId = employerId.Value,
            EmployerName = employer.Value.Identity.DisplayName,
            IsVerified = employer.Value.IsVerified,

            DraftCount = await CountByStatusAsync(employerId, JobStatus.Draft, cancellationToken),
            OpenCount = await _postings.CountOpenByEmployerAsync(employerId, cancellationToken),
            ClosedCount = await CountByStatusAsync(
                employerId, JobStatus.Expired, cancellationToken),
            FilledCount = await CountByStatusAsync(
                employerId, JobStatus.Filled, cancellationToken),

            TotalApplicationCount = byStatus.Values.Sum(),
            ActiveApplicationCount = activeCount,
            UnviewedApplicationCount = unviewedCount,
            StaleApplicationCount = staleCount,
            ApplicationsByStatus = byStatus.ToDictionary(
                entry => JobWireNames.ToWire(entry.Key),
                entry => entry.Value,
                StringComparer.Ordinal),

            UpcomingInterviewCount = upcomingInterviews.Count,
            OutstandingOfferCount = outstandingOffers.Count,

            Responsiveness = EmployerMapper.ToResponsiveness(employer.Value),

            RecentJobs = [.. recent.Items.Select(posting =>
                JobMapper.ToCard(posting, employer.Value))]
        });
    }

    /// <summary>
    /// The employer named, or the caller's sole one. Null when they belong to several and did
    /// not say which — the platform must not guess which company somebody meant.
    /// </summary>
    private async Task<EmployerProfileId?> ResolveEmployerIdAsync(
        GetEmployerDashboardQuery query, CancellationToken cancellationToken)
    {
        if (query.EmployerId is { } explicitId)
        {
            return EmployerProfileId.From(explicitId);
        }

        var sole = await _authorization.ResolveSoleEmployerAsync(cancellationToken);

        return sole?.Id;
    }

    private async Task<int> CountByStatusAsync(
        EmployerProfileId employerId, JobStatus status, CancellationToken cancellationToken) =>
        await _postings.CountAsync(
            new JobQueryFilter
            {
                EmployerId = employerId,
                ViewerEmployerId = employerId,
                IncludeNonPublic = true,
                Statuses = [status]
            },
            cancellationToken);
}
