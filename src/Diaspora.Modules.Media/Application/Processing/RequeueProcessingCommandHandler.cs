using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Domain.Processing;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Processing;

/// <summary>
/// Plans and runs a fresh job over an asset.
/// <para>
/// A new job rather than a retry of the old one, because the interesting case is a deployment
/// that gained a capability: the old job's steps are already recorded as unsupported, and
/// re-running them would overwrite the evidence of what the deployment looked like at the
/// time.
/// </para>
/// </summary>
public sealed class RequeueProcessingCommandHandler
    : ICommandHandler<RequeueProcessingCommand, ProcessingJobResponse>
{
    private readonly IProcessingJobRepository _jobs;
    private readonly IMediaAssetRepository _assets;
    private readonly MediaProcessingPipeline _pipeline;
    private readonly IUnitOfWork _unitOfWork;

    public RequeueProcessingCommandHandler(
        IProcessingJobRepository jobs,
        IMediaAssetRepository assets,
        MediaProcessingPipeline pipeline,
        IUnitOfWork unitOfWork)
    {
        _jobs = jobs;
        _assets = assets;
        _pipeline = pipeline;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ProcessingJobResponse>> Handle(
        RequeueProcessingCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var asset = await _assets.GetAsync(MediaId.From(request.MediaId), cancellationToken);

        if (asset is null)
        {
            return Result.Failure<ProcessingJobResponse>(MediaErrors.AssetNotFound);
        }

        var job = ProcessingJob.Plan(asset.Id, MediaProcessingPipeline.Plan(asset.Kind));
        await _jobs.AddAsync(job, cancellationToken);

        await _pipeline.ExecuteAsync(job, asset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ProcessingMapper.ToResponse(job));
    }
}
