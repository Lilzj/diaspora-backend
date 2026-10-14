using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Analytics;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Pipelines;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.Modules.Jobs.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Jobs.Application.Postings;

/// <summary>
/// Creates a posting, its hiring pipeline and its analytics rollup.
/// <para>
/// Every step is load-bearing: confirm the caller may recruit for this employer, hold them to the
/// draft ceiling, validate the category against the catalogue, allocate a free deep-link handle,
/// build the aggregate, and give it a board to work applications on.
/// </para>
/// <para>
/// The pipeline and the analytics rollup are created here rather than lazily on first use,
/// because "create it if it is missing" is a race two concurrent applications will lose.
/// </para>
/// </summary>
public sealed class CreateJobCommandHandler : ICommandHandler<CreateJobCommand, JobResponse>
{
    private readonly IJobPostingRepository _postings;
    private readonly IHiringPipelineRepository _pipelines;
    private readonly IJobAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly JobCategoryResolver _categories;
    private readonly JobSlugAllocator _slugs;
    private readonly IOptions<JobOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateJobCommandHandler(
        IJobPostingRepository postings,
        IHiringPipelineRepository pipelines,
        IJobAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        JobAuthorizationService authorization,
        JobCategoryResolver categories,
        JobSlugAllocator slugs,
        IOptions<JobOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _postings = postings;
        _pipelines = pipelines;
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _categories = categories;
        _slugs = slugs;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<JobResponse>> Handle(
        CreateJobCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;
        var employerId = EmployerProfileId.From(request.EmployerId);

        var employer = await _authorization.RequireEmployerAsync(
            employerId, EmployerMemberRole.Recruiter, cancellationToken);

        if (employer.IsFailure)
        {
            return Result.Failure<JobResponse>(employer.Error);
        }

        if (!employer.Value.CanRecruit)
        {
            return Result.Failure<JobResponse>(JobErrors.EmployerSuspended);
        }

        // An abuse bound rather than an editorial one: it is drafts a script can create for free
        // that are counted, not published postings.
        var openCount = await _postings.CountOpenByEmployerAsync(employerId, cancellationToken);

        if (openCount >= _options.Value.MaxOpenPostingsPerEmployer)
        {
            return Result.Failure<JobResponse>(JobErrors.TooManyPostings);
        }

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

        var category = await _categories.ResolveActiveAsync(
            request.CategoryKey, cancellationToken);

        if (category.IsFailure)
        {
            return Result.Failure<JobResponse>(category.Error);
        }

        var jobType = JobWireNames.ParseJobType(request.JobType);
        var experience = JobWireNames.ParseExperienceLevel(request.ExperienceLevel);

        if (jobType is null || experience is null)
        {
            return Result.Failure<JobResponse>(JobErrors.CategoryKeyInvalid);
        }

        // The area id old app builds still send is accepted and not written (14 Oct 2026).
        var created = JobLocationContext.Create(
            request.CountryCode, request.RegionName, request.CityName, areaId: null);

        if (created.IsFailure)
        {
            return Result.Failure<JobResponse>(created.Error);
        }

        // Not filed under a catalogue area (14 Oct 2026, Phase 1 of retiring the place
        // catalogue): a posting is found by its pin, its country and the town its employer typed.
        var location = created;

        var arrangement = WorkArrangement.Create(
            JobWireNames.ParseWorkplaceType(request.WorkplaceType) ?? WorkplaceType.OnSite,
            request.OffersVisaSponsorship,
            request.OffersRelocationAssistance);

        if (arrangement.IsFailure)
        {
            return Result.Failure<JobResponse>(arrangement.Error);
        }

        var slug = await _slugs.AllocatePostingSlugAsync(
            request.Slug,
            title.Value.Value,
            employer.Value.Identity.DisplayName,
            location.Value.CityName,
            cancellationToken);

        if (slug.IsFailure)
        {
            return Result.Failure<JobResponse>(slug.Error);
        }

        var now = _clock.UtcNow;

        var posting = JobPosting.Create(
            JobPostingId.From(_guids.NewSequentialGuid()),
            employerId,
            _authorization.CallerId!,
            title.Value,
            slug.Value,
            description.Value,
            category.Value.Key,
            jobType.Value,
            experience.Value,
            location.Value,
            arrangement.Value,
            now);

        if (posting.IsFailure)
        {
            return Result.Failure<JobResponse>(posting.Error);
        }

        var pinned = posting.Value.SetCoordinates(request.Latitude, request.Longitude);

        if (pinned.IsFailure)
        {
            return Result.Failure<JobResponse>(pinned.Error);
        }

        await _postings.AddAsync(posting.Value, cancellationToken);

        // Created eagerly rather than on first use: "create it if missing" is a race two
        // concurrent applications will lose.
        var pipeline = HiringPipeline.CreateDefault(
            HiringPipelineId.From(_guids.NewSequentialGuid()),
            posting.Value.Id,
            employerId,
            now);

        if (pipeline.IsFailure)
        {
            return Result.Failure<JobResponse>(pipeline.Error);
        }

        await _pipelines.AddAsync(pipeline.Value, cancellationToken);

        await _analytics.AddAsync(
            JobAnalytics.Create(
                JobAnalyticsId.From(_guids.NewSequentialGuid()),
                posting.Value.Id,
                employerId,
                now),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(JobMapper.ToResponse(
            posting.Value, employer.Value, now, canManage: true));
    }
}
