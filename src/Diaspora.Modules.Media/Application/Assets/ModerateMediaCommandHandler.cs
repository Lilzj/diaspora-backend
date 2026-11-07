using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>
/// Applies a moderator's decision to a file.
/// <para>
/// <b>No access guard, on purpose.</b> The guard answers "may this caller manage this file", and the
/// answer for a moderator is now no — which is the point. The permission on the command is the
/// gate, the reason is required, and the decision is one of two that are both recorded on the file.
/// </para>
/// <para>
/// <b>Nobody moderates their own upload.</b> A moderator who wants their own file gone deletes it
/// through the member's route like anybody else.
/// </para>
/// </summary>
public sealed class ModerateMediaCommandHandler
    : ICommandHandler<ModerateMediaCommand, MediaModerationResponse>
{
    private const string Hide = "Hide";
    private const string Remove = "Remove";

    private readonly IMediaAssetRepository _assets;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public ModerateMediaCommandHandler(
        IMediaAssetRepository assets,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _assets = assets;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<MediaModerationResponse>> Handle(
        ModerateMediaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var moderator = _currentUser.UserId;

        if (moderator is null)
        {
            return Result.Failure<MediaModerationResponse>(MediaErrors.NotAuthenticated);
        }

        var action = request.Request.Action?.Trim() ?? string.Empty;
        var reason = request.Request.Reason?.Trim() ?? string.Empty;

        if (reason.Length == 0)
        {
            return Result.Failure<MediaModerationResponse>(MediaErrors.ModerationReasonRequired);
        }

        var applied =
            string.Equals(action, Hide, StringComparison.OrdinalIgnoreCase) ? Hide
            : string.Equals(action, Remove, StringComparison.OrdinalIgnoreCase) ? Remove
            : null;

        if (applied is null)
        {
            return Result.Failure<MediaModerationResponse>(MediaErrors.ModerationActionUnknown);
        }

        var asset = await _assets.GetSummaryAsync(MediaId.From(request.MediaId), cancellationToken);

        if (asset is null)
        {
            return Result.Failure<MediaModerationResponse>(MediaErrors.AssetNotFound);
        }

        if (asset.UploadedBy == moderator)
        {
            return Result.Failure<MediaModerationResponse>(MediaErrors.ModerationOfOwnFile);
        }

        var decided = applied == Hide
            ? asset.Withhold(reason)
            : asset.Remove(moderator, reason);

        if (decided.IsFailure)
        {
            return Result.Failure<MediaModerationResponse>(decided.Error);
        }

        if (applied == Remove)
        {
            // The member's own deletion path: the soft-delete conversion stamps who and when, and
            // the bytes go on the sweeper's schedule.
            _assets.Remove(asset);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new MediaModerationResponse
        {
            MediaId = asset.Id.Value,
            Action = applied,
            Status = MediaWireNames.ToWire(asset.Status),
            Reason = asset.FailureReason ?? reason,
            UploadedBy = asset.UploadedBy.Value
        });
    }
}
