using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Application.Uploads;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>
/// Attaches an asset to an owning entity.
/// <para>
/// Only the member who uploaded it, or someone they granted management to, may attach it.
/// Without that check, any member could attach someone else's photograph to their own listing
/// — an image-theft vector that costs nothing to close here and is impossible to close later.
/// </para>
/// </summary>
public sealed class AttachMediaCommandHandler
    : ICommandHandler<AttachMediaCommand, MediaAssetResponse>
{
    private readonly IMediaAssetRepository _assets;
    private readonly MediaAccessGuard _guard;
    private readonly MediaMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public AttachMediaCommandHandler(
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
        AttachMediaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var asset = await _assets.GetAsync(
            MediaId.From(request.Request.MediaId), cancellationToken);

        if (asset is null)
        {
            return Result.Failure<MediaAssetResponse>(MediaErrors.AssetNotFound);
        }

        var allowed = await _guard.CanManageAsync(asset, cancellationToken);

        if (allowed.IsFailure)
        {
            return Result.Failure<MediaAssetResponse>(allowed.Error);
        }

        var owner = StartUploadCommandHandler.ResolveOwner(
            request.Request.OwnerType, request.Request.OwnerId);

        if (owner.IsFailure)
        {
            return Result.Failure<MediaAssetResponse>(owner.Error);
        }

        var attached = asset.AttachTo(owner.Value, request.Request.Role);

        if (attached.IsFailure)
        {
            return Result.Failure<MediaAssetResponse>(attached.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(_mapper.ToResponse(asset));
    }
}
