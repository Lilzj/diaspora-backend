using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Processing;

/// <summary>Lifts a quarantine and returns the asset to processing.</summary>
public sealed class ReleaseQuarantineCommandHandler
    : ICommandHandler<ReleaseQuarantineCommand, MediaAssetResponse>
{
    private readonly IMediaAssetRepository _assets;
    private readonly MediaMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ReleaseQuarantineCommandHandler(
        IMediaAssetRepository assets,
        MediaMapper mapper,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _assets = assets;
        _mapper = mapper;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<MediaAssetResponse>> Handle(
        ReleaseQuarantineCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var asset = await _assets.GetAsync(MediaId.From(request.MediaId), cancellationToken);

        if (asset is null)
        {
            return Result.Failure<MediaAssetResponse>(MediaErrors.AssetNotFound);
        }

        // The releasing member's identifier becomes the scan result's "scanner", so the record
        // says a human made this call rather than presenting it as a clean machine verdict.
        var released = asset.ReleaseFromQuarantine(
            _clock.UtcNow, $"manual:{_currentUser.UserId?.Value}");

        if (released.IsFailure)
        {
            return Result.Failure<MediaAssetResponse>(released.Error);
        }

        // A file released from a moderator's withhold that the scanner has not cleared yet stays in
        // processing, and the scan pipeline finishes it. Only a file safe to serve becomes ready.
        var ready = asset.ScanResult.IsSafeToServe ? asset.MarkReady() : Result.Success();

        if (ready.IsFailure)
        {
            return Result.Failure<MediaAssetResponse>(ready.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(_mapper.ToResponse(asset));
    }
}
