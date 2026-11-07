using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Infrastructure.Moderation;

/// <summary>
/// A member's file, as a moderation case sees it (7 Nov 2026).
/// <para>
/// <b>The only way staff take a member's file down now.</b> The member's own edit and delete routes
/// refuse staff, and the stage-2 <c>POST /media/{id}/moderation</c> is gone: a decision is made on
/// the Trust case and carried out here, in the same transaction, with the moderator's note kept on
/// the file as the reason.
/// </para>
/// <para>
/// <b>A removal withholds; it never deletes.</b> A hide pending review and a removal both use the
/// quarantine floor (<see cref="MediaAsset.Withhold"/>): the bytes are served to nobody, the file
/// joins the quarantine queue, and the case knows which of the two it is. Deleting on a decision
/// would start the byte sweeper and make a removal impossible to reverse on appeal.
/// </para>
/// <para>
/// A restore is the release path: the file goes back to processing and, when its scan verdict is
/// safe, to members. A file the scanner itself quarantined stays where the scanner put it: lifting a
/// moderation decision is not a finding about malware, and only the release route, a person's call
/// that the scanner was wrong, overrules one.
/// </para>
/// </summary>
public sealed class MediaModerationTarget : IModerationTarget
{
    private readonly IMediaAssetRepository _assets;
    private readonly IClock _clock;

    public MediaModerationTarget(IMediaAssetRepository assets, IClock clock)
    {
        _assets = assets;
        _clock = clock;
    }

    public string ContentType => ModerationContentTypes.Media;

    public string Area => ModerationAreas.Media;

    public async Task<ModerationTargetSnapshot?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        var asset = await _assets.GetSummaryAsync(MediaId.From(contentId), cancellationToken);

        // The uploader answers for the file and is the one told.
        return asset is null
            ? null
            : new ModerationTargetSnapshot(
                asset.Id.Value,
                asset.UploadedBy.Value,
                string.IsNullOrWhiteSpace(asset.Caption) ? asset.FileName.Value : asset.Caption,
                asset.Status.ToString());
    }

    public async Task<Result<ModerationTargetChange>> ApplyAsync(
        Guid contentId,
        ModerationOutcome outcome,
        ModerationActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var asset = await _assets.GetAsync(MediaId.From(contentId), cancellationToken);

        if (asset is null)
        {
            return Result.Failure<ModerationTargetChange>(MediaErrors.AssetNotFound);
        }

        var before = asset.Status.ToString();

        var applied = outcome switch
        {
            // Already withheld by a decision (a removal confirmed after its hide): it stays as it
            // is, with the reason the uploader was first given.
            ModerationOutcome.HidePendingReview or ModerationOutcome.Remove
                => asset.Status is MediaAssetStatus.Quarantined && asset.FailureReason is not null
                    ? Result.Success()
                    : asset.Withhold(actor.Note),
            ModerationOutcome.Restore => Release(asset, actor),
            // A file carries no automatic flag, so a case closed with no action changes nothing.
            ModerationOutcome.NoAction => Result.Success(),
            _ => Result.Failure(Error.Validation(
                MediaErrorCodes.ModerationActionUnknown, $"'{outcome}' is not a decision."))
        };

        return applied.IsFailure
            ? Result.Failure<ModerationTargetChange>(applied.Error)
            : Result.Success(new ModerationTargetChange(before, asset.Status.ToString()));
    }

    /// <summary>
    /// The release route's path, for a file a decision withheld: back to processing, and to members
    /// once its scan verdict is safe. A file that is not withheld has nothing to restore, and one the
    /// scanner flagged stays quarantined.
    /// </summary>
    private Result Release(MediaAsset asset, ModerationActor actor)
    {
        if (asset.Status is not MediaAssetStatus.Quarantined || asset.ScanResult.RequiresQuarantine)
        {
            return Result.Success();
        }

        var released = asset.ReleaseFromQuarantine(_clock.UtcNow, $"moderation:{actor.CaseId}");

        if (released.IsFailure)
        {
            return released;
        }

        // Not scanned yet: it stays in processing, and the scan pipeline finishes it.
        return asset.ScanResult.IsSafeToServe ? asset.MarkReady() : Result.Success();
    }
}
