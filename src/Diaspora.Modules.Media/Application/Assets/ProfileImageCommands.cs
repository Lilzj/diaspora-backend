using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Assets;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// A member's profile photo and creator cover (20 Oct 2026).
//
// The app said "Photos are not stored yet" because nothing could make a member's own upload
// readable by anyone else: every asset is created Private, and no module claimed the UserProfile
// owner type with a policy. A profile image is the case MediaVisibility.Public was written for
// ("Listing photos, avatars, guide covers"), so these commands claim the asset for the caller's
// own profile and widen it — and narrow it again when the member replaces or removes it.
//
// Called in-process by Identity (avatar) and Creator (cover), the way Providers calls
// AttachMediaCommand: Media learns nothing about either.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Claims one of the caller's own image uploads for their profile and makes it public.
/// <para>
/// <b>Stricter than attaching.</b> Attaching admits anyone the uploader granted management to; a
/// profile image must be the caller's own upload, because it is about to be shown to everyone under
/// the caller's name. Neither admits staff on a staff permission.
/// </para>
/// </summary>
[Auditable]
// Dispatched from inside an authorised handler, never from a route of its own —
// ProfileController ([Authorize] at the class) and Creator's cover handlers. The caller is
// present in both.
[RequireAuthentication(
    "Dispatched from inside an already-authorised handler, so the caller is present and the media " +
    "is their own.")]
public sealed record PublishProfileImageCommand : ICommand<ProfileImageResponse>
{
    public required Guid MediaId { get; init; }

    /// <summary><see cref="MediaReferenceRoles.Avatar"/> or <see cref="MediaReferenceRoles.Cover"/>.</summary>
    public required string Role { get; init; }
}

/// <summary>
/// Makes a profile image private again once the member no longer shows it.
/// <para>
/// A no-op — never a failure — when the asset is gone, is not the caller's profile image, or has
/// since been claimed for the other role: a member who used one photograph as both avatar and
/// cover and then removes the avatar must keep their cover.
/// </para>
/// </summary>
[Auditable]
// Same two dispatch sites as PublishProfileImageCommand.
[RequireAuthentication(
    "Dispatched from inside an already-authorised handler, so the caller is present and the media " +
    "is their own.")]
public sealed record WithdrawProfileImageCommand : ICommand
{
    public required Guid MediaId { get; init; }

    public required string Role { get; init; }
}

public sealed class PublishProfileImageCommandHandler
    : ICommandHandler<PublishProfileImageCommand, ProfileImageResponse>
{
    private readonly IMediaAssetRepository _assets;
    private readonly MediaAccessGuard _guard;
    private readonly MediaUrlFactory _urls;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public PublishProfileImageCommandHandler(
        IMediaAssetRepository assets,
        MediaAccessGuard guard,
        MediaUrlFactory urls,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _assets = assets;
        _guard = guard;
        _urls = urls;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ProfileImageResponse>> Handle(
        PublishProfileImageCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var caller = _currentUser.UserId;

        if (caller is null)
        {
            return Result.Failure<ProfileImageResponse>(MediaErrors.NotAuthenticated);
        }

        if (!ProfileImageRoles.IsKnown(request.Role))
        {
            return Result.Failure<ProfileImageResponse>(MediaErrors.ReferenceOwnerInvalid);
        }

        var asset = await _assets.GetAsync(MediaId.From(request.MediaId), cancellationToken);

        if (asset is null)
        {
            return Result.Failure<ProfileImageResponse>(MediaErrors.AssetNotFound);
        }

        var allowed = await _guard.CanManageAsync(asset, cancellationToken);

        if (allowed.IsFailure)
        {
            return Result.Failure<ProfileImageResponse>(allowed.Error);
        }

        // See the command's summary: management is not enough, it must be the caller's upload.
        if (asset.UploadedBy != caller)
        {
            return Result.Failure<ProfileImageResponse>(MediaErrors.OwnershipRequired);
        }

        if (asset.Kind is not MediaKind.Image)
        {
            return Result.Failure<ProfileImageResponse>(MediaErrors.ProfileImageNotAnImage);
        }

        var attached = asset.AttachTo(
            MediaOwner.Create(MediaOwnerType.UserProfile, caller.Value), request.Role);

        if (attached.IsFailure)
        {
            return Result.Failure<ProfileImageResponse>(attached.Error);
        }

        var widened = asset.ChangeVisibility(MediaVisibility.Public);

        if (widened.IsFailure)
        {
            return Result.Failure<ProfileImageResponse>(widened.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ProfileImageResponse
        {
            MediaId = asset.Id.Value,
            Role = asset.Role ?? request.Role,
            Url = _urls.ForVariant(asset, MediaVariantPurpose.Medium)
        });
    }
}

public sealed class WithdrawProfileImageCommandHandler : ICommandHandler<WithdrawProfileImageCommand>
{
    private readonly IMediaAssetRepository _assets;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public WithdrawProfileImageCommandHandler(
        IMediaAssetRepository assets, ICurrentUser currentUser, IUnitOfWork unitOfWork)
    {
        _assets = assets;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(WithdrawProfileImageCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var caller = _currentUser.UserId;

        if (caller is null)
        {
            return Result.Failure(MediaErrors.NotAuthenticated);
        }

        var asset = await _assets.GetAsync(MediaId.From(request.MediaId), cancellationToken);

        var isThisProfileImage = asset is not null
            && asset.Status is not MediaAssetStatus.Deleted
            && asset.Owner.Type is MediaOwnerType.UserProfile
            && asset.Owner.Id == caller.Value
            && string.Equals(asset.Role, request.Role, StringComparison.OrdinalIgnoreCase)
            && asset.Visibility is MediaVisibility.Public;

        if (!isThisProfileImage)
        {
            return Result.Success();
        }

        var narrowed = asset!.ChangeVisibility(MediaVisibility.Private);

        if (narrowed.IsFailure)
        {
            // Quarantined: already served to nobody, which is what withdrawing wanted.
            return Result.Success();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>The two roles a profile image can hold.</summary>
public static class ProfileImageRoles
{
    public const string Avatar = MediaReferenceRoles.Avatar;

    public const string Cover = MediaReferenceRoles.Cover;

    public static bool IsKnown(string? role) =>
        string.Equals(role, Avatar, StringComparison.Ordinal)
        || string.Equals(role, Cover, StringComparison.Ordinal);
}
