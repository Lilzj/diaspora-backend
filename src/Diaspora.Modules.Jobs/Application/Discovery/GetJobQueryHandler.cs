using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.Saved;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Discovery;

/// <summary>
/// Reads one job.
/// <para>
/// Hydrates the provider and guide cards from B7 and B8 at read time rather than from copies, so
/// an agency that renamed itself or a guide that was unpublished is never misrepresented by a
/// posting written months ago. Both fail soft: a directory outage renders bare references, not a
/// failed page.
/// </para>
/// </summary>
public sealed class GetJobQueryHandler : IQueryHandler<GetJobQuery, JobResponse>
{
    private readonly IJobPostingRepository _postings;
    private readonly IEmployerProfileRepository _employers;
    private readonly IJobApplicationRepository _applications;
    private readonly ISavedJobRepository _saved;
    private readonly JobAuthorizationService _authorization;
    private readonly JobProviderResolver _providers;
    private readonly JobGuideResolver _guides;
    private readonly IClock _clock;

    public GetJobQueryHandler(
        IJobPostingRepository postings,
        IEmployerProfileRepository employers,
        IJobApplicationRepository applications,
        ISavedJobRepository saved,
        JobAuthorizationService authorization,
        JobProviderResolver providers,
        JobGuideResolver guides,
        IClock clock)
    {
        _postings = postings;
        _employers = employers;
        _applications = applications;
        _saved = saved;
        _authorization = authorization;
        _providers = providers;
        _guides = guides;
        _clock = clock;
    }

    public async Task<Result<JobResponse>> Handle(
        GetJobQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var posting = await ResolveAsync(query, cancellationToken);

        if (posting is null)
        {
            return Result.Failure<JobResponse>(JobErrors.PostingNotFound);
        }

        // Not-found rather than forbidden. A 403 on a hidden posting confirms it exists.
        if (!await _authorization.CanSeeAsync(posting, cancellationToken))
        {
            return Result.Failure<JobResponse>(JobErrors.PostingNotFound);
        }

        var employer = await _employers.GetForReadAsync(posting.EmployerId, cancellationToken);

        if (employer is null)
        {
            return Result.Failure<JobResponse>(JobErrors.EmployerNotFound);
        }

        var providers = await _providers.ResolveAsync(
            [.. posting.ProviderReferences.Select(reference => reference.ProviderId)],
            cancellationToken);

        var guides = await _guides.ResolveAsync(
            [.. posting.GuideReferences.Select(reference => reference.GuideId)],
            cancellationToken);

        var (hasApplied, isSaved) = await ResolveViewerStateAsync(posting, cancellationToken);

        var isOnTeam = await _authorization.IsMemberOfAsync(
            posting.EmployerId, cancellationToken);

        // Only the team manages the job. Staff are told apart by `canModerate`: since 7 Nov 2026
        // the owner's routes refuse them, so a `canManage` that said yes to staff would draw edit
        // and close controls that fail on press.
        return Result.Success(JobMapper.ToResponse(
            posting,
            employer,
            _clock.UtcNow,
            canManage: isOnTeam,
            isOnEmployerTeam: isOnTeam,
            hasApplied,
            isSaved,
            providers,
            guides,
            canModerate: _authorization.IsStaff));
    }

    private async Task<JobPosting?> ResolveAsync(
        GetJobQuery query, CancellationToken cancellationToken)
    {
        if (query.JobId is { } id)
        {
            return await _postings.GetForReadAsync(
                JobPostingId.From(id), cancellationToken);
        }

        var slug = JobSlug.Create(query.Slug);

        return slug.IsFailure
            ? null
            : await _postings.GetBySlugAsync(slug.Value, cancellationToken);
    }

    /// <summary>
    /// Whether the viewer has applied and whether they saved it. Both null for an anonymous
    /// reader, which is what lets the client render a sign-in prompt rather than a wrong state.
    /// </summary>
    private async Task<(bool? HasApplied, bool? IsSaved)> ResolveViewerStateAsync(
        JobPosting posting, CancellationToken cancellationToken)
    {
        if (_authorization.CallerId is not { } viewer)
        {
            return (null, null);
        }

        var applied = await _applications.FindAppliedPostingIdsAsync(
            viewer, [posting.Id], cancellationToken);

        var saved = await _saved.FindSavedPostingIdsAsync(
            viewer, [posting.Id], cancellationToken);

        return (applied.Contains(posting.Id.Value), saved.Contains(posting.Id.Value));
    }
}
