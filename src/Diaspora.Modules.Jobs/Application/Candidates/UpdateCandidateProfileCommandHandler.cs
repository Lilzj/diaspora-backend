using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Candidates;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Candidates;

/// <summary>Updates a candidate profile.</summary>
public sealed class UpdateCandidateProfileCommandHandler
    : ICommandHandler<UpdateCandidateProfileCommand, CandidateProfileResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly IClock _clock;

    public UpdateCandidateProfileCommandHandler(
        IUnitOfWork unitOfWork, JobAuthorizationService authorization, IClock clock)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<CandidateProfileResponse>> Handle(
        UpdateCandidateProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var candidate = await _authorization.RequireCandidateAsync(cancellationToken);

        if (candidate.IsFailure)
        {
            return Result.Failure<CandidateProfileResponse>(candidate.Error);
        }

        var request = command.Request;

        var headline = CandidateHeadline.Create(request.Headline, request.Summary);

        if (headline.IsFailure)
        {
            return Result.Failure<CandidateProfileResponse>(headline.Error);
        }

        var experience = JobWireNames.ParseExperienceLevel(request.ExperienceLevel);

        if (experience is null)
        {
            return Result.Failure<CandidateProfileResponse>(JobErrors.HeadlineInvalid);
        }

        var expectation = SalaryExpectation.Create(
            request.ExpectedSalaryMinimum,
            request.ExpectedSalaryCurrency,
            JobWireNames.ParseSalaryPeriod(request.ExpectedSalaryPeriod),
            request.ExpectedSalaryFlexible);

        if (expectation.IsFailure)
        {
            return Result.Failure<CandidateProfileResponse>(expectation.Error);
        }

        var updated = candidate.Value.UpdateProfile(
            headline.Value,
            request.CountryCode,
            request.CityName,
            experience.Value,
            JobWireNames.ParseRemotePreference(request.RemotePreference)
                ?? RemotePreference.Flexible,
            expectation.Value,
            request.NoticePeriodDays,
            request.IsOpenToWork,
            request.BaseRevision);

        if (updated.IsFailure)
        {
            return Result.Failure<CandidateProfileResponse>(updated.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var now = _clock.UtcNow;

        return Result.Success(CandidateMapper.ToResponse(
            candidate.Value, DateOnly.FromDateTime(now.UtcDateTime), forOwner: true));
    }
}
