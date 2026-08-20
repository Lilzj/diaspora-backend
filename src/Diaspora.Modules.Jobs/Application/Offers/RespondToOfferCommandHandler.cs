using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Offers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Offers;

/// <summary>
/// The candidate answers an offer.
/// <para>
/// <b>Acceptance is the only path in the module that fills a vacancy</b>, and it does four things
/// at once: answers the offer, moves the application to <c>Accepted</c>, takes a seat on the
/// posting — closing it if that was the last one — and records the acceptance against the
/// employer. All in one transaction, because a hire recorded in three of those places and not
/// the fourth is wrong everywhere it was not.
/// </para>
/// </summary>
public sealed class RespondToOfferCommandHandler
    : ICommandHandler<RespondToOfferCommand, JobOfferResponse>
{
    private readonly IJobOfferRepository _offers;
    private readonly IJobApplicationRepository _applications;
    private readonly IJobPostingRepository _postings;
    private readonly IEmployerProfileRepository _employers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public RespondToOfferCommandHandler(
        IJobOfferRepository offers,
        IJobApplicationRepository applications,
        IJobPostingRepository postings,
        IEmployerProfileRepository employers,
        IUnitOfWork unitOfWork,
        JobAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _offers = offers;
        _applications = applications;
        _postings = postings;
        _employers = employers;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<JobOfferResponse>> Handle(
        RespondToOfferCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var offer = await _offers.GetAsync(
            JobOfferId.From(command.OfferId), cancellationToken);

        if (offer is null)
        {
            return Result.Failure<JobOfferResponse>(JobErrors.OfferNotFound);
        }

        var application = await _applications.GetAsync(offer.ApplicationId, cancellationToken);

        if (application is null)
        {
            return Result.Failure<JobOfferResponse>(JobErrors.ApplicationNotFound);
        }

        // The candidate whose application it is, and nobody else.
        var owner = _authorization.RequireOwner(application.ApplicantUserId);

        if (owner.IsFailure)
        {
            return Result.Failure<JobOfferResponse>(owner.Error);
        }

        var now = _clock.UtcNow;

        var answered = command.Request.Accept
            ? offer.Accept(now, owner.Value, command.Request.Reason)
            : offer.Reject(now, owner.Value, command.Request.Reason);

        if (answered.IsFailure)
        {
            return Result.Failure<JobOfferResponse>(answered.Error);
        }

        application.NoteOfferAnswered(
            _guids.NewSequentialGuid(), command.Request.Accept, owner.Value, now);

        var moved = application.MoveTo(
            command.Request.Accept ? ApplicationStatus.Accepted : ApplicationStatus.Rejected,
            now,
            owner.Value,
            command.Request.Reason);

        if (moved.IsFailure)
        {
            return Result.Failure<JobOfferResponse>(moved.Error);
        }

        var employer = await _employers.GetAsync(offer.EmployerId, cancellationToken);
        var posting = await _postings.GetAsync(offer.PostingId, cancellationToken);

        if (command.Request.Accept)
        {
            // Takes a seat, and closes the posting if it was the last one.
            var filled = posting?.FillVacancy(application.Id, now);

            if (filled is { IsFailure: true })
            {
                return Result.Failure<JobOfferResponse>(filled.Error);
            }

            employer?.RecordOfferAccepted();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(OfferMapper.ToResponse(
            offer,
            posting?.Title.Value ?? string.Empty,
            employer?.Identity.DisplayName ?? string.Empty,
            now));
    }
}
