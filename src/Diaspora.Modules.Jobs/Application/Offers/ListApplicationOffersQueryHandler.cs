using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Offers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Offers;

/// <summary>
/// Reads the offers on an application, for whichever side of the table is asking.
/// </summary>
public sealed class ListApplicationOffersQueryHandler
    : IQueryHandler<ListApplicationOffersQuery, IReadOnlyList<JobOfferResponse>>
{
    private readonly IJobOfferRepository _offers;
    private readonly IJobApplicationRepository _applications;
    private readonly IJobPostingRepository _postings;
    private readonly IEmployerProfileRepository _employers;
    private readonly JobAuthorizationService _authorization;
    private readonly IClock _clock;

    public ListApplicationOffersQueryHandler(
        IJobOfferRepository offers,
        IJobApplicationRepository applications,
        IJobPostingRepository postings,
        IEmployerProfileRepository employers,
        JobAuthorizationService authorization,
        IClock clock)
    {
        _offers = offers;
        _applications = applications;
        _postings = postings;
        _employers = employers;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<JobOfferResponse>>> Handle(
        ListApplicationOffersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var application = await _applications.GetForReadAsync(
            JobApplicationId.From(query.ApplicationId), cancellationToken);

        if (application is null)
        {
            return Result.Failure<IReadOnlyList<JobOfferResponse>>(JobErrors.ApplicationNotFound);
        }

        // The employer's team and the candidate, and nobody else. An offer names a salary.
        // `ApplicationNotFound` for an outsider rather than a forbidden result, for the same reason
        // as the interview list: 403 on a guessable identifier is itself a disclosure.
        var isEmployer = await _authorization.IsMemberOfAsync(application.EmployerId, cancellationToken);
        var isCandidate = _authorization.RequireOwner(application.ApplicantUserId).IsSuccess;

        if (!isEmployer && !isCandidate)
        {
            return Result.Failure<IReadOnlyList<JobOfferResponse>>(JobErrors.ApplicationNotFound);
        }

        var offers = await _offers.GetForApplicationAsync(application.Id, cancellationToken);

        if (offers.Count == 0)
        {
            return Result.Success<IReadOnlyList<JobOfferResponse>>([]);
        }

        var posting = await _postings.GetForReadAsync(application.PostingId, cancellationToken);
        var employer = await _employers.GetForReadAsync(application.EmployerId, cancellationToken);

        var jobTitle = posting?.Title.Value ?? string.Empty;
        var employerName = employer?.Identity.DisplayName ?? string.Empty;

        // `now` is passed to the mapper because expiry is computed at read time — an offer that
        // lapsed while nobody was looking must read as lapsed, not as outstanding.
        var now = _clock.UtcNow;

        return Result.Success<IReadOnlyList<JobOfferResponse>>(
            [.. offers
                .OrderByDescending(offer => offer.CreatedAt)
                .Select(offer => OfferMapper.ToResponse(offer, jobTitle, employerName, now))]);
    }
}
