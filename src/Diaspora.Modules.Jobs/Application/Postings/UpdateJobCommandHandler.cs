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
    private readonly JobAreaFiling _filing;
    private readonly IJobPostingRepository _postings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly IClock _clock;

    public UpdateJobCommandHandler(
        JobAreaFiling filing,
        IJobPostingRepository postings,
        IUnitOfWork unitOfWork,
        JobAuthorizationService authorization,
        IClock clock)
    {
        _filing = filing;
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

        var created = JobLocationContext.Create(
            request.CountryCode, request.RegionName, request.CityName, request.AreaId);

        if (created.IsFailure)
        {
            return Result.Failure<JobResponse>(created.Error);
        }

        // Filed under the catalogue node the posting names — 16 Sep 2026. A posting carries no
        // coordinates and, until now, no path either: every job on the platform was invisible to
        // the map's cluster counts and to every town's job count. An edit rebuilds the place from
        // the request, so it is filed afresh each time rather than keeping a path for a town the
        // employer may just have changed.
        var location = Result.Success(await _filing.FileAsync(created.Value, cancellationToken));

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

        // Omitted coordinates leave the pin alone, so a client that has never sent one cannot
        // clear a pin a newer client set.
        if (request.Latitude is not null || request.Longitude is not null)
        {
            var pinned = posting.SetCoordinates(request.Latitude, request.Longitude);

            if (pinned.IsFailure)
            {
                return Result.Failure<JobResponse>(pinned.Error);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(JobMapper.ToResponse(
            posting, employer.Value, _clock.UtcNow, canManage: true));
    }
}
