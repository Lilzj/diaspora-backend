using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Postings;

/// <summary>Updates a posting.</summary>
public sealed class UpdateJobCommandHandler : ICommandHandler<UpdateJobCommand, JobResponse>
{
    private readonly IJobPostingRepository _postings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly IClock _clock;

    public UpdateJobCommandHandler(
        IJobPostingRepository postings,
        IUnitOfWork unitOfWork,
        JobAuthorizationService authorization,
        IClock clock)
    {
        _postings = postings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<JobResponse>> Handle(
        UpdateJobCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var posting = await _postings.GetAsync(
            JobPostingId.From(command.JobId), cancellationToken);

        if (posting is null)
        {
            return Result.Failure<JobResponse>(JobErrors.PostingNotFound);
        }

        var employer = await _authorization.RequirePostingAccessAsync(
            posting, EmployerMemberRole.Recruiter, cancellationToken);

        if (employer.IsFailure)
        {
            return Result.Failure<JobResponse>(employer.Error);
        }

        var request = command.Request;

        var title = JobTitle.Create(request.Title);

        if (title.IsFailure)
        {
            return Result.Failure<JobResponse>(title.Error);
        }

        var description = JobDescription.Create(request.Description, request.Summary);

        if (description.IsFailure)
        {
            return Result.Failure<JobResponse>(description.Error);
        }

        var jobType = JobWireNames.ParseJobType(request.JobType);
        var experience = JobWireNames.ParseExperienceLevel(request.ExperienceLevel);

        if (jobType is null || experience is null)
        {
            return Result.Failure<JobResponse>(JobErrors.CategoryKeyInvalid);
        }

        var location = JobLocationContext.Create(
            request.CountryCode, request.RegionName, request.CityName, request.AreaId);

        if (location.IsFailure)
        {
            return Result.Failure<JobResponse>(location.Error);
        }

        var arrangement = WorkArrangement.Create(
            JobWireNames.ParseWorkplaceType(request.WorkplaceType) ?? WorkplaceType.OnSite,
            request.OffersVisaSponsorship,
            request.OffersRelocationAssistance,
            request.OnSiteDaysPerWeek,
            request.RemotePolicy);

        if (arrangement.IsFailure)
        {
            return Result.Failure<JobResponse>(arrangement.Error);
        }

        var salary = SalaryRange.Create(
            request.SalaryMinimum,
            request.SalaryMaximum,
            request.SalaryCurrency,
            JobWireNames.ParseSalaryPeriod(request.SalaryPeriod),
            request.SalaryNegotiable);

        if (salary.IsFailure)
        {
            return Result.Failure<JobResponse>(salary.Error);
        }

        var updated = posting.UpdateDetails(
            title.Value,
            description.Value,
            jobType.Value,
            experience.Value,
            JobWireNames.ParseEducationLevel(request.EducationLevel) ?? EducationLevel.None,
            location.Value,
            arrangement.Value,
            salary.Value,
            JobWireNames.ParseVisibility(request.Visibility) ?? posting.Visibility,
            request.VacancyCount,
            request.RequiresResume,
            request.BaseRevision,
            _authorization.CallerId);

        if (updated.IsFailure)
        {
            return Result.Failure<JobResponse>(updated.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(JobMapper.ToResponse(
            posting, employer.Value, _clock.UtcNow, canManage: true));
    }
}
