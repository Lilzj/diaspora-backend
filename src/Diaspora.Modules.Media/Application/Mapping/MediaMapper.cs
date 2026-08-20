using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;

namespace Diaspora.Modules.Media.Application.Mapping;

/// <summary>
/// Projects assets onto their wire contract.
/// <para>
/// Hand-written rather than convention-mapped, because the interesting parts are exactly the
/// ones a convention would get wrong: <see cref="MediaAssetResponse.Url"/> is minted, not
/// copied; <see cref="MediaAssetResponse.ThumbnailUrl"/> is derived from a collection;
/// storage keys and provider names must never appear at all. A mapper that reflected over
/// properties would happily publish <c>Location.Key</c>, and the abstraction would leak on the
/// day somebody added a property.
/// </para>
/// </summary>
public sealed class MediaMapper
{
    private readonly MediaUrlFactory _urls;

    public MediaMapper(MediaUrlFactory urls)
    {
        _urls = urls;
    }

    public MediaAssetResponse ToResponse(MediaAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        return new MediaAssetResponse
        {
            MediaId = asset.Id.Value,
            Kind = MediaWireNames.ToWire(asset.Kind),
            Status = MediaWireNames.ToWire(asset.Status),
            Visibility = MediaWireNames.ToWire(asset.Visibility),
            ContentType = asset.ContentType.Value,

            // The name the member gave it, not the sanitised storage form. A member who
            // uploaded "Passeport – Côte d'Ivoire.pdf" should see that name back.
            FileName = asset.FileName.Original,

            SizeBytes = asset.Size.Bytes,
            Url = _urls.ForOriginal(asset),
            ThumbnailUrl = _urls.ForThumbnail(asset),
            Metadata = ToMetadata(asset),
            Variants = asset.Variants
                .OrderBy(variant => variant.Purpose)
                .Select(variant => ToVariant(asset, variant))
                .ToList(),
            AltText = asset.AltText,
            Caption = asset.Caption,
            Reference = ToReference(asset),
            ScanStatus = MediaWireNames.ToWire(asset.ScanResult.Status),
            RequiresSignedAccess = asset.RequiresSignedAccess,
            CreatedAt = asset.CreatedAt
        };
    }

    public IReadOnlyList<MediaAssetResponse> ToResponses(IEnumerable<MediaAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        return assets.Select(ToResponse).ToList();
    }

    private MediaVariantResponse ToVariant(MediaAsset asset, MediaVariant variant) =>
        new()
        {
            Purpose = MediaWireNames.ToWire(variant.Purpose),
            Url = _urls.ForVariant(asset, variant.Purpose),
            ContentType = variant.ContentType.Value,
            SizeBytes = variant.Size.Bytes,
            Width = variant.Dimensions?.Width,
            Height = variant.Dimensions?.Height
        };

    private static MediaMetadataResponse ToMetadata(MediaAsset asset) =>
        new()
        {
            Width = asset.Metadata.Dimensions?.Width,
            Height = asset.Metadata.Dimensions?.Height,
            DurationSeconds = asset.Metadata.Duration?.TotalSeconds,
            PageCount = asset.Metadata.PageCount,
            Orientation = asset.Metadata.Orientation,
            BlurHash = asset.Metadata.BlurHash,
            ContentHash = asset.Hash.Value
        };

    private static MediaReferenceResponse? ToReference(MediaAsset asset) =>
        asset.Owner.IsAssigned
            ? new MediaReferenceResponse
            {
                MediaId = asset.Id.Value,
                OwnerType = MediaWireNames.ToWire(asset.Owner.Type),
                OwnerId = asset.Owner.Id,
                Role = asset.Role ?? string.Empty,
                Position = 0
            }
            : null;
}
