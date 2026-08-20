using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Offers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Offers;

/// <summary>
/// Drafts and optionally extends an offer.
/// <para>
/// A live offer already with the candidate blocks a second — revising terms means superseding,
/// which the caller does by withdrawing first. Two live offers on one application would leave the
/// candidate unable to tell which one they are accepting.
/// </para>
/// </summary>
public sealed class ExtendOfferCommandHandler
    : ICommandHandler<ExtendOfferCommand, JobOfferResponse>
{
    private readonly IJobOfferRepository _offers;
    private readonly IJobApplicationRepository _applications;
    private readonly IJobPostingRepository _postings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public ExtendOfferCommandHandler(
        IJobOfferRepository offers,
        IJobApplicationRepository applications,
        IJobPostingRepository postings,
        IUnitOfWork unitOfWork,
        JobAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _offers = offers;
        _applications = applications;
        _postings = postings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<JobOfferResponse>> Handle(
        ExtendOfferCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var application = await _applications.GetAsync(
            JobApplicationId.From(command.ApplicationId), cancellationToken);

        if (application is null)
        {
            return Result.Failure<JobOfferResponse>(JobErrors.ApplicationNotFound);
        }

        var employer = await _authorization.RequireEmployerAsync(
            application.EmployerId, EmployerMemberRole.Recruiter, cancellationToken);

        if (employer.IsFailure)
        {
            return Result.Failure<JobOfferResponse>(employer.Error);
        }

        var live = await _offers.GetLiveForApplicationAsync(application.Id, cancellationToken);

        if (live is not null)
        {
            return Result.Failure<JobOfferResponse>(JobErrors.OfferAlreadyExtended);
        }

        var request = command.Request;
        var now = _clock.UtcNow;

        var period = JobWireNames.ParseSalaryPeriod(request.SalaryPeriod);

        if (period is null)
        {
            return Result.Failure<JobOfferResponse>(JobErrors.OfferTermsInvalid);
        }

        var terms = OfferTerms.Create(
            request.SalaryAmount,
            request.CurrencyCode,
            period.Value,
            request.ExpiresAt,
            now,
            request.StartDate,
            request.BenefitsSummary,
            request.Notes,
            request.IncludesRelocation,
            request.IncludesVisaSponsorship);

        if (terms.IsFailure)
        {
            return Result.Failure<JobOfferResponse>(terms.Error);
        }

        var offer = JobOffer.Draft(
            JobOfferId.From(_guids.NewSequentialGuid()),
            application.Id,
            application.PostingId,
            application.EmployerId,
            application.CandidateId,
            terms.Value,
            now);

        if (offer.IsFailure)
        {
            return Result.Failure<JobOfferResponse>(offer.Error);
        }

        await _offers.AddAsync(offer.Value, cancellationToken);

        if (request.ExtendImmediately)
        {
            var extended = offer.Value.Extend(now, _authorization.CallerId);

            if (extended.IsFailure)
            {
                return Result.Failure<JobOfferResponse>(extended.Error);
            }

            // The application moves only when the candidate can actually see the offer. A draft
            // is an internal artefact and must not change what the candidate is told.
            if (application.Status != ApplicationStatus.OfferExtended)
            {
                var moved = application.MoveTo(
                    ApplicationStatus.OfferExtended, now, _authorization.CallerId);

                if (moved.IsFailure)
                {
                    return Result.Failure<JobOfferResponse>(moved.Error);
                }
            }

            application.NoteOfferExtended(
                _guids.NewSequentialGuid(), _authorization.CallerId, now);

            employer.Value.RecordOfferExtended();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var posting = await _postings.GetForReadAsync(application.PostingId, cancellationToken);

        return Result.Success(OfferMapper.ToResponse(
            offer.Value,
            posting?.Title.Value ?? string.Empty,
            employer.Value.Identity.DisplayName,
            now));
    }
}
