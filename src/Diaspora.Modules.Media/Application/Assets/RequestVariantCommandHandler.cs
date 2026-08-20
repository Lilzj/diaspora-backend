using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Domain.Processing;
using Diaspora.Modules.Media.Extensibility.Processing;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>
/// Produces one rung of the ladder on demand.
/// <para>
/// Idempotent: asking for a rendition that already exists returns the asset unchanged rather
/// than regenerating it. That is what makes this safe to call from a client that does not know
/// what already exists — which is every client.
/// </para>
/// <para>
/// Refuses with a distinct error when no processor can produce the rendition, rather than
/// queueing work nothing will ever perform. A client that asked for a thumbnail on a
/// deployment with no codec should be told so, not left polling.
/// </para>
/// </summary>
public sealed class RequestVariantCommandHandler
    : ICommandHandler<RequestVariantCommand, MediaAssetResponse>
{
    private readonly IMediaAssetRepository _assets;
    private readonly IProcessingJobRepository _jobs;
    private readonly MediaAccessGuard _guard;
    private readonly MediaProcessorRegistry _processors;
    private readonly MediaProcessingPipeline _pipeline;
    private readonly MediaMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public RequestVariantCommandHandler(
        IMediaAssetRepository assets,
        IProcessingJobRepository jobs,
        MediaAccessGuard guard,
        MediaProcessorRegistry processors,
        MediaProcessingPipeline pipeline,
        MediaMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _assets = assets;
        _jobs = jobs;
        _guard = guard;
        _processors = processors;
        _pipeline = pipeline;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<MediaAssetResponse>> Handle(
        RequestVariantCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var asset = await _assets.GetAsync(MediaId.From(request.MediaId), cancellationToken);

        if (asset is null)
        {
            return Result.Failure<MediaAssetResponse>(MediaErrors.AssetNotFound);
        }

        var allowed = await _guard.CanReadContentAsync(asset, cancellationToken);

        if (allowed.IsFailure)
        {
            return Result.Failure<MediaAssetResponse>(MediaErrors.AssetNotFound);
        }

        var purpose = MediaWireNames.Parse<MediaVariantPurpose>(
            request.Request.Variant,
            Error.Validation(MediaErrorCodes.VariantNotSupported, "That is not a variant."));

        if (purpose.IsFailure)
        {
            return Result.Failure<MediaAssetResponse>(purpose.Error);
        }

        if (asset.HasVariant(purpose.Value))
        {
            return Result.Success(_mapper.ToResponse(asset));
        }

        var operation = OperationFor(purpose.Value);

        if (operation is null)
        {
            return Result.Failure<MediaAssetResponse>(MediaErrors.VariantNotSupported);
        }

        if (!_processors.Supports(asset.Kind, operation.Value))
        {
            return Result.Failure<MediaAssetResponse>(MediaErrors.TransformationUnavailable);
        }

        var job = ProcessingJob.Plan(asset.Id, [operation.Value]);
        await _jobs.AddAsync(job, cancellationToken);

        await _pipeline.ExecuteAsync(job, asset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(_mapper.ToResponse(asset));
    }

    private static ProcessingOperation? OperationFor(MediaVariantPurpose purpose) => purpose switch
    {
        MediaVariantPurpose.Thumbnail or
        MediaVariantPurpose.Small or
        MediaVariantPurpose.Medium or
        MediaVariantPurpose.Large => ProcessingOperation.GenerateThumbnails,

        MediaVariantPurpose.Preview => ProcessingOperation.GeneratePreview,
        MediaVariantPurpose.Poster => ProcessingOperation.ExtractPosterFrame,
        MediaVariantPurpose.Transcoded => ProcessingOperation.TranscodeVideo,
        MediaVariantPurpose.ExtractedText => ProcessingOperation.RecognizeText,

        // The original is not derived; asking for it is a caller error, not a transformation.
        _ => null
    };
}
