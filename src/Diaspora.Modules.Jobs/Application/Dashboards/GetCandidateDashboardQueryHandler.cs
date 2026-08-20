using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Alerts;
using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Modules.Jobs.Domain.Candidates;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Interviews;
using Diaspora.Modules.Jobs.Domain.Offers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.Saved;
using Diaspora.Modules.Jobs.Extensibility.Recommendations;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Dashboards;

/// <summary>
/// Builds the candidate dashboard.
/// <para>
/// <b>A member with no candidate profile still gets a dashboard.</b> It is mostly empty and it
/// prompts them to create one — returning "not found" to somebody who has saved jobs and set
/// alerts but never filled in a profile would be the platform refusing to show them their own
/// activity.
/// </para>
/// <para>
/// The order is the candidate's, not the platform's: offers first because they expire,
/// interviews next because they are attended, then applications, then saved jobs closing soon.
/// Recommendations come last — leading with suggestions would be optimising for engagement over
/// the thing the member actually came to do.
/// </para>
/// </summary>
public sealed class GetCandidateDashboardQueryHandler
    : IQueryHandler<GetCandidateDashboardQuery, CandidateDashboardResponse>
{
    private const int RecentApplicationCount = 5;
    private const int RecommendationCount = 6;
    private const int ClosingSoonCount = 5;

    private readonly ICandidateProfileRepository _candidates;
    private readonly IJobApplicationRepository _applications;
    private readonly IJobOfferRepository _offers;
    private readonly IInterviewRepository _interviews;
    private readonly ISavedJobRepository _saved;
    private readonly IJobAlertRepository _alerts;
    private readonly IJobPostingRepository _postings;
    private readonly IEmployerProfileRepository _employers;
    private readonly IEnumerable<IJobRecommendationProvider> _recommenders;
    private readonly JobAuthorizationService _authorization;
    private readonly IClock _clock;

    public GetCandidateDashboardQueryHandler(
        ICandidateProfileRepository candidates,
        IJobApplicationRepository applications,
        IJobOfferRepository offers,
        IInterviewRepository interviews,
        ISavedJobRepository saved,
        IJobAlertRepository alerts,
        IJobPostingRepository postings,
        IEmployerProfileRepository employers,
        IEnumerable<IJobRecommendationProvider> recommenders,
        JobAuthorizationService authorization,
        IClock clock)
    {
        _candidates = candidates;
        _applications = applications;
        _offers = offers;
        _interviews = interviews;
        _saved = saved;
        _alerts = alerts;
        _postings = postings;
        _employers = employers;
        _recommenders = recommenders;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<CandidateDashboardResponse>> Handle(
        GetCandidateDashboardQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CandidateDashboardResponse>(caller.Error);
        }

        var now = _clock.UtcNow;

        // Null is fine. A member with no profile still has saved jobs and alerts to show.
        var profile = await _candidates.GetByUserForReadAsync(caller.Value, cancellationToken);

        var alerts = await _alerts.GetForUserAsync(caller.Value, cancellationToken);

        var scope = new ApplicationQueryFilter { ApplicantUserId = caller.Value };

        var totalApplications = await _applications.CountAsync(scope, cancellationToken);

        var activeApplications = await _applications.CountAsync(
            scope with { ActiveOnly = true }, cancellationToken);

        var unviewedApplications = await _applications.CountAsync(
            scope with { UnviewedOnly = true, ActiveOnly = true }, cancellationToken);

        var recent = await _applications.QueryAsync(
            scope, cursor: null, RecentApplicationCount, cancellationToken);

        var byStatus = new Dictionary<string, int>(StringComparer.Ordinal);

        var offers = profile is null
            ? []
            : await _offers.GetOutstandingForCandidateAsync(profile.Id, cancellationToken);

        var interviews = profile is null
            ? []
            : await _interviews.GetUpcomingForCandidateAsync(
                profile.Id, now, 10, cancellationToken);

        var savedCount = profile is null
            ? 0
            : await _saved.CountForCandidateAsync(profile.Id, cancellationToken);

        return Result.Success(new CandidateDashboardResponse
        {
            CandidateId = profile?.Id.Value,
            ProfileCompletenessPercent = profile?.CompletenessPercent,

            OutstandingOffers = await BuildOffersAsync(offers, now, cancellationToken),
            UpcomingInterviews = await BuildInterviewsAsync(interviews, cancellationToken),

            ActiveApplicationCount = activeApplications,
            TotalApplicationCount = totalApplications,
            UnviewedApplicationCount = unviewedApplications,
            ApplicationsByStatus = byStatus,
            RecentApplications = await BuildApplicationCardsAsync(
                recent.Items, now, cancellationToken),

            SavedJobCount = savedCount,
            SavedClosingSoon = await BuildClosingSoonAsync(profile, cancellationToken),
            ActiveAlertCount = alerts.Count(alert => alert.IsActive),
            Recommended = await BuildRecommendationsAsync(
                caller.Value, profile?.Id, cancellationToken)
        });
    }

    private async Task<IReadOnlyList<JobOfferResponse>> BuildOffersAsync(
        IReadOnlyList<Domain.Offers.JobOffer> offers,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (offers.Count == 0)
        {
            return [];
        }

        var postings = (await _postings.GetManyAsync(
                [.. offers.Select(offer => offer.PostingId).Distinct()], cancellationToken))
            .ToDictionary(posting => posting.Id.Value);

        var employers = (await _employers.GetManyAsync(
                [.. offers.Select(offer => offer.EmployerId).Distinct()], cancellationToken))
            .ToDictionary(employer => employer.Id.Value);

        return [.. offers.Select(offer => OfferMapper.ToResponse(
            offer,
            postings.GetValueOrDefault(offer.PostingId.Value)?.Title.Value ?? string.Empty,
            employers.GetValueOrDefault(offer.EmployerId.Value)?.Identity.DisplayName
                ?? string.Empty,
            now))];
    }

    private async Task<IReadOnlyList<InterviewResponse>> BuildInterviewsAsync(
        IReadOnlyList<Domain.Interviews.Interview> interviews,
        CancellationToken cancellationToken)
    {
        if (interviews.Count == 0)
        {
            return [];
        }

        var postings = (await _postings.GetManyAsync(
                [.. interviews.Select(interview => interview.PostingId).Distinct()],
                cancellationToken))
            .ToDictionary(posting => posting.Id.Value);

        var employers = (await _employers.GetManyAsync(
                [.. interviews.Select(interview => interview.EmployerId).Distinct()],
                cancellationToken))
            .ToDictionary(employer => employer.Id.Value);

        // forEmployer: false — the candidate never sees the panel's internal notes.
        return [.. interviews.Select(interview => InterviewMapper.ToResponse(
            interview,
            postings.GetValueOrDefault(interview.PostingId.Value)?.Title.Value ?? string.Empty,
            employers.GetValueOrDefault(interview.EmployerId.Value)?.Identity.DisplayName
                ?? string.Empty,
            forEmployer: false))];
    }

    private async Task<IReadOnlyList<ApplicationCardResponse>> BuildApplicationCardsAsync(
        IReadOnlyList<JobApplication> applications,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (applications.Count == 0)
        {
            return [];
        }

        var postings = (await _postings.GetManyAsync(
                [.. applications.Select(application => application.PostingId).Distinct()],
                cancellationToken))
            .ToDictionary(posting => posting.Id.Value);

        var employers = (await _employers.GetManyAsync(
                [.. applications.Select(application => application.EmployerId).Distinct()],
                cancellationToken))
            .ToDictionary(employer => employer.Id.Value);

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
                employer?.Branding.LogoMediaId);
        })];
    }

    /// <summary>
    /// Saved jobs whose closing date is near. The nudge that actually helps somebody apply in
    /// time, which is the whole reason the saved list has a reminder flag at all.
    /// </summary>
    private async Task<IReadOnlyList<JobCardResponse>> BuildClosingSoonAsync(
        CandidateProfile? profile, CancellationToken cancellationToken)
    {
        if (profile is null)
        {
            return [];
        }

        var saved = await _saved.GetForCandidateAsync(
            profile.Id, cursor: null, 50, cancellationToken);

        if (saved.Items.Count == 0)
        {
            return [];
        }

        var postings = await _postings.GetManyAsync(
            [.. saved.Items.Select(entry => entry.PostingId)], cancellationToken);

        var closingSoon = postings
            .Where(posting => posting.ClosingAt is not null && posting.IsDiscoverable)
            .OrderBy(posting => posting.ClosingAt)
            .Take(ClosingSoonCount)
            .ToList();

        return await BuildJobCardsAsync(closingSoon, cancellationToken);
    }

    private async Task<IReadOnlyList<JobCardResponse>> BuildRecommendationsAsync(
        SharedKernel.Identity.UserId userId,
        CandidateProfileId? candidateId,
        CancellationToken cancellationToken)
    {
        var recommender = _recommenders.FirstOrDefault();

        if (recommender is null)
        {
            return [];
        }

        var recommendations = await recommender.RecommendAsync(
            userId, candidateId, RecommendationCount, cancellationToken);

        if (recommendations.Count == 0)
        {
            return [];
        }

        var reasons = recommendations.ToDictionary(
            recommendation => recommendation.JobId,
            recommendation => recommendation.Reason);

        var postings = await _postings.GetManyAsync(
            [.. recommendations.Select(
                recommendation => JobPostingId.From(recommendation.JobId))],
            cancellationToken);

        var employers = (await _employers.GetManyAsync(
                [.. postings.Select(posting => posting.EmployerId).Distinct()],
                cancellationToken))
            .ToDictionary(employer => employer.Id.Value);

        // Every card carries the reason it was suggested. Never unexplained.
        return [.. postings.Select(posting => JobMapper.ToCard(
            posting,
            employers.GetValueOrDefault(posting.EmployerId.Value),
            recommendationReason: reasons.GetValueOrDefault(posting.Id.Value)))];
    }

    private async Task<IReadOnlyList<JobCardResponse>> BuildJobCardsAsync(
        List<JobPosting> postings, CancellationToken cancellationToken)
    {
        if (postings.Count == 0)
        {
            return [];
        }

        var employers = (await _employers.GetManyAsync(
                [.. postings.Select(posting => posting.EmployerId).Distinct()],
                cancellationToken))
            .ToDictionary(employer => employer.Id.Value);

        return [.. postings.Select(posting => JobMapper.ToCard(
            posting, employers.GetValueOrDefault(posting.EmployerId.Value)))];
    }
}
