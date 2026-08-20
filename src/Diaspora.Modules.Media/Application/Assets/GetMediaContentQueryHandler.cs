using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Modules.Media.Extensibility.Storage;
using Diaspora.Modules.Media.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>
/// Serves an asset's bytes, after deciding — twice — that the caller may have them.
/// <para>
/// <b>Two independent gates, and both must pass.</b> A signed token, when one is presented, is
/// checked first. Then the access guard runs anyway. A token is a convenience for a client
/// that cannot send a header; it is not a bypass, and an asset that has been quarantined since
/// the token was minted is refused even though the signature is perfectly valid. That is the
/// property a provider-presigned URL cannot offer, and the reason this module mints its own.
/// </para>
/// <para>
/// Falls back to the original when a requested rendition does not exist. A client asking for a
/// thumbnail on a deployment with no image codec gets a picture — larger than it wanted —
/// rather than a broken image. Degrading in quality beats degrading in function.
/// </para>
/// </summary>
public sealed class GetMediaContentQueryHandler
    : IQueryHandler<GetMediaContentQuery, MediaContentResult>
{
    private readonly IMediaAssetRepository _assets;
    private readonly MediaAccessGuard _guard;
    private readonly SignedUrlFactory _signedUrls;
    private readonly StorageProviderRegistry _storage;
    private readonly ICurrentUser _currentUser;

    public GetMediaContentQueryHandler(
        IMediaAssetRepository assets,
        MediaAccessGuard guard,
        SignedUrlFactory signedUrls,
        StorageProviderRegistry storage,
        ICurrentUser currentUser)
    {
        _assets = assets;
        _guard = guard;
        _signedUrls = signedUrls;
        _storage = storage;
        _currentUser = currentUser;
    }

    public async Task<Result<MediaContentResult>> Handle(
        GetMediaContentQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var asset = await _assets.GetAsync(MediaId.From(request.MediaId), cancellationToken);

        if (asset is null)
        {
            return Result.Failure<MediaContentResult>(MediaErrors.AssetNotFound);
        }

        var purpose = MediaWireNames.ParseOrDefault(
            request.Variant, MediaVariantPurpose.Original);

        if (!string.IsNullOrWhiteSpace(request.Signature))
        {
            var token = new SignedMediaToken
            {
                MediaId = asset.Id,
                Purpose = purpose,
                Audience = request.AudienceUserId is { } id ? UserId.From(id) : null,
                ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(request.ExpiresAtUnixSeconds ?? 0),
                Signature = request.Signature
            };

            var valid = _signedUrls.Validate(token, _currentUser.UserId);

            if (valid.IsFailure)
            {
                return Result.Failure<MediaContentResult>(valid.Error);
            }
        }
        else
        {
            // No token: the caller must be able to read it on their own authority.
            var allowed = await _guard.CanReadContentAsync(asset, cancellationToken);

            if (allowed.IsFailure)
            {
                return Result.Failure<MediaContentResult>(allowed.Error);
            }
        }

        // Runs even when a token was presented and verified. Quarantine, deletion and a
        // revoked grant all post-date the token, and a signature is not a licence to ignore
        // them.
        var stillAllowed = await _guard.CanReadContentAsync(asset, cancellationToken);

        if (stillAllowed.IsFailure)
        {
            return Result.Failure<MediaContentResult>(stillAllowed.Error);
        }

        var variant = purpose is MediaVariantPurpose.Original ? null : asset.FindVariant(purpose);

        var location = variant?.Location ?? asset.Location;
        var contentType = variant?.ContentType ?? asset.ContentType;
        var length = variant?.Size.Bytes ?? asset.Size.Bytes;

        var provider = _storage.Resolve(location.Provider);

        if (provider.IsFailure)
        {
            return Result.Failure<MediaContentResult>(provider.Error);
        }

        var stream = request.RangeOffset > 0 || request.RangeLength is not null
            ? await provider.Value.OpenRangeAsync(
                location.Container, location.Key, request.RangeOffset, request.RangeLength,
                cancellationToken)
            : await provider.Value.OpenReadAsync(
                location.Container, location.Key, cancellationToken);

        if (stream is null)
        {
            return Result.Failure<MediaContentResult>(MediaErrors.StorageObjectMissing);
        }

        MediaTelemetry.BytesServed.Add(length,
            new KeyValuePair<string, object?>("variant", MediaWireNames.ToWire(purpose)));

        return Result.Success(new MediaContentResult(
            stream,
            contentType.Value,
            asset.FileName.Value,
            variant?.Size.Bytes ?? asset.Size.Bytes,

            // Derived from the content hash rather than from the provider, so it survives a
            // storage migration and is identical across instances.
            asset.Hash.ToEntityTag(),
            asset.Visibility is MediaVisibility.Public,
            asset.ModifiedAt ?? asset.CreatedAt));
    }
}
