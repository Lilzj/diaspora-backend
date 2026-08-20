using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Collections;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Collections;

/// <summary>
/// Reads a collection, filtered to the assets the caller may actually see.
/// <para>
/// One query for every asset in the collection, not one per item. A gallery of forty
/// photographs is the exact shape that turns a naive implementation into forty round trips,
/// and it is a shape a member chooses.
/// </para>
/// </summary>
public sealed class GetCollectionQueryHandler
    : IQueryHandler<GetCollectionQuery, MediaCollectionResponse>
{
    private readonly IMediaCollectionRepository _collections;
    private readonly IMediaAssetRepository _assets;
    private readonly MediaAccessGuard _guard;
    private readonly CollectionMapper _mapper;

    public GetCollectionQueryHandler(
        IMediaCollectionRepository collections,
        IMediaAssetRepository assets,
        MediaAccessGuard guard,
        CollectionMapper mapper)
    {
        _collections = collections;
        _assets = assets;
        _guard = guard;
        _mapper = mapper;
    }

    public async Task<Result<MediaCollectionResponse>> Handle(
        GetCollectionQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var collection = await _collections.GetAsync(
            MediaCollectionId.From(request.CollectionId), cancellationToken);

        if (collection is null)
        {
            return Result.Failure<MediaCollectionResponse>(MediaErrors.CollectionNotFound);
        }

        var ids = collection.Items.Select(item => item.MediaId).ToList();
        var assets = await _assets.GetManyAsync(ids, cancellationToken);

        var visible = new Dictionary<SharedKernel.Identity.MediaId, MediaAsset>();

        foreach (var asset in assets)
        {
            var allowed = await _guard.CanReadMetadataAsync(asset, cancellationToken);

            if (allowed.IsSuccess)
            {
                visible[asset.Id] = asset;
            }
        }

        return Result.Success(_mapper.ToResponse(collection, visible));
    }
}
