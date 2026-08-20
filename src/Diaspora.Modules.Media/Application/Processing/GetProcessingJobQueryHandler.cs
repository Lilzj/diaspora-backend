using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Domain.Processing;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Processing;

/// <summary>Reads an asset's latest job, for a caller who may see the asset.</summary>
public sealed class GetProcessingJobQueryHandler
    : IQueryHandler<GetProcessingJobQuery, ProcessingJobResponse>
{
    private readonly IProcessingJobRepository _jobs;
    private readonly IMediaAssetRepository _assets;
    private readonly MediaAccessGuard _guard;

    public GetProcessingJobQueryHandler(
        IProcessingJobRepository jobs, IMediaAssetRepository assets, MediaAccessGuard guard)
    {
        _jobs = jobs;
        _assets = assets;
        _guard = guard;
    }

    public async Task<Result<ProcessingJobResponse>> Handle(
        GetProcessingJobQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mediaId = MediaId.From(request.MediaId);
        var asset = await _assets.GetSummaryAsync(mediaId, cancellationToken);

        if (asset is null)
        {
            return Result.Failure<ProcessingJobResponse>(MediaErrors.AssetNotFound);
        }

        var allowed = await _guard.CanReadMetadataAsync(asset, cancellationToken);

        if (allowed.IsFailure)
        {
            return Result.Failure<ProcessingJobResponse>(MediaErrors.AssetNotFound);
        }

        var job = await _jobs.GetLatestForMediaAsync(mediaId, cancellationToken);

        return job is null
            ? Result.Failure<ProcessingJobResponse>(MediaErrors.ProcessingJobNotFound)
            : Result.Success(ProcessingMapper.ToResponse(job));
    }
}
