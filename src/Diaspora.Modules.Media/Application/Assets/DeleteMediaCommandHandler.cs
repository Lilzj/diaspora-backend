using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>Soft-deletes an asset the caller may manage.</summary>
public sealed class DeleteMediaCommandHandler : ICommandHandler<DeleteMediaCommand>
{
    private readonly IMediaAssetRepository _assets;
    private readonly MediaAccessGuard _guard;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteMediaCommandHandler(
        IMediaAssetRepository assets,
        MediaAccessGuard guard,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _assets = assets;
        _guard = guard;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        DeleteMediaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure(MediaErrors.NotAuthenticated);
        }

        var asset = await _assets.GetSummaryAsync(MediaId.From(request.MediaId), cancellationToken);

        if (asset is null)
        {
            return Result.Failure(MediaErrors.AssetNotFound);
        }

        var allowed = await _guard.CanManageAsync(asset, cancellationToken);

        if (allowed.IsFailure)
        {
            return allowed;
        }

        var deleted = asset.Delete(userId);

        if (deleted.IsFailure)
        {
            return deleted;
        }

        // The platform's soft-delete conversion turns this into a status change plus a
        // deleted-at stamp, and leaves the asset's owned value objects and variants intact.
        //
        // The bytes go on the sweeper's schedule, which is what keeps a delete from blocking
        // on a 512 MB object-store call.
        _assets.Remove(asset);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
