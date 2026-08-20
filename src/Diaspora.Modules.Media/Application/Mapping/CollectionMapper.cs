using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Collections;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Media.Application.Mapping;

/// <summary>
/// Projects collections onto their wire contract.
/// <para>
/// Takes the assets as a lookup rather than fetching them, because the caller has already
/// loaded them in one query. A mapper that fetched per item would turn a gallery of forty
/// photographs into forty round trips — the classic N+1, in the one place where N is a number
/// a member chose.
/// </para>
/// <para>
/// An item whose asset is missing is skipped rather than rendered empty. That happens when an
/// asset was deleted and the collection has not been tidied, and a gallery with a hole in it
/// is a better outcome than one with a broken image in it.
/// </para>
/// </summary>
public sealed class CollectionMapper
{
    private readonly MediaMapper _media;

    public CollectionMapper(MediaMapper media)
    {
        _media = media;
    }

    public MediaCollectionResponse ToResponse(
        MediaCollection collection, IReadOnlyDictionary<MediaId, MediaAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(assets);

        var items = new List<MediaCollectionItemResponse>();

        foreach (var item in collection.OrderedItems)
        {
            if (!assets.TryGetValue(item.MediaId, out var asset))
            {
                continue;
            }

            items.Add(new MediaCollectionItemResponse
            {
                MediaId = item.MediaId.Value,
                Position = item.Position,
                Caption = item.Caption,
                Media = _media.ToResponse(asset)
            });
        }

        return new MediaCollectionResponse
        {
            CollectionId = collection.Id.Value,
            Kind = MediaWireNames.ToWire(collection.Kind),
            OwnerType = MediaWireNames.ToWire(collection.Owner.Type),
            OwnerId = collection.Owner.Id,
            Name = collection.Name,
            Capacity = collection.Capacity,
            Items = items
        };
    }
}
