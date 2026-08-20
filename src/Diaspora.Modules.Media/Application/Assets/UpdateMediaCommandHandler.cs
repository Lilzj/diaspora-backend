using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>Applies a description or visibility change to an asset the caller may manage.</summary>
public sealed class UpdateMediaCommandHandler
    : ICommandHandler<UpdateMediaCommand, MediaAssetResponse>
{
    private readonly IMediaAssetRepository _assets;
    private readonly MediaAccessGuard _guard;
    private readonly MediaMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateMediaCommandHandler(
        IMediaAssetRepository assets,
        MediaAccessGuard guard,
        MediaMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _assets = assets;
        _guard = guard;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<MediaAssetResponse>> Handle(
        UpdateMediaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var asset = await _assets.GetAsync(MediaId.From(request.MediaId), cancellationToken);

        if (asset is null)
        {
            return Result.Failure<MediaAssetResponse>(MediaErrors.AssetNotFound);
        }

        var allowed = await _guard.CanManageAsync(asset, cancellationToken);

        if (allowed.IsFailure)
        {
            return Result.Failure<MediaAssetResponse>(allowed.Error);
        }

        asset.Describe(request.Request.AltText, request.Request.Caption);

        if (!string.IsNullOrWhiteSpace(request.Request.Visibility))
        {
            var visibility = MediaWireNames.Parse<MediaVisibility>(
                request.Request.Visibility,
                Error.Validation(
                    MediaErrorCodes.AssetAccessDenied, "That is not a visibility level."));

            if (visibility.IsFailure)
            {
                return Result.Failure<MediaAssetResponse>(visibility.Error);
            }

            // The domain refuses to make a verification document public, at any tier, by any
            // caller. That rule lives in the aggregate rather than here precisely so it cannot
            // be forgotten by a second write path.
            var changed = asset.ChangeVisibility(visibility.Value);

            if (changed.IsFailure)
            {
                return Result.Failure<MediaAssetResponse>(changed.Error);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(_mapper.ToResponse(asset));
    }
}
