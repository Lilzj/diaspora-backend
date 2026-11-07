using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Collections;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Collections;

/// <summary>Applies one arrangement change to a collection the caller owns.</summary>
public sealed class ModifyCollectionCommandHandler
    : ICommandHandler<ModifyCollectionCommand, MediaCollectionResponse>
{
    private readonly IMediaCollectionRepository _collections;
    private readonly IMediaAssetRepository _assets;
    private readonly MediaAccessGuard _guard;
    private readonly CollectionMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public ModifyCollectionCommandHandler(
        IMediaCollectionRepository collections,
        IMediaAssetRepository assets,
        MediaAccessGuard guard,
        CollectionMapper mapper,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _collections = collections;
        _assets = assets;
        _guard = guard;
        _mapper = mapper;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<MediaCollectionResponse>> Handle(
        ModifyCollectionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<MediaCollectionResponse>(MediaErrors.NotAuthenticated);
        }

        var collection = await _collections.GetAsync(
            MediaCollectionId.From(request.CollectionId), cancellationToken);

        if (collection is null)
        {
            return Result.Failure<MediaCollectionResponse>(MediaErrors.CollectionNotFound);
        }

        // The member who made it, and nobody else. A staff permission is not a reason to rearrange
        // a member's gallery; until 7 Nov 2026 `media:administer` was, with no reason or notice.
        if (collection.CreatedByUser != userId)
        {
            return Result.Failure<MediaCollectionResponse>(MediaErrors.OwnershipRequired);
        }

        var applied = await ApplyAsync(collection, request, cancellationToken);

        if (applied.IsFailure)
        {
            return Result.Failure<MediaCollectionResponse>(applied.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var assets = await _assets.GetManyAsync(
            collection.Items.Select(item => item.MediaId).ToList(), cancellationToken);

        return Result.Success(
            _mapper.ToResponse(collection, assets.ToDictionary(asset => asset.Id)));
    }

    private async Task<Result> ApplyAsync(
        MediaCollection collection,
        ModifyCollectionCommand request,
        CancellationToken cancellationToken)
    {
        switch (request.Operation.Trim().ToLowerInvariant())
        {
            case "add":
                if (request.MediaId is not { } toAdd)
                {
                    return Result.Failure(MediaErrors.CollectionItemNotFound);
                }

                // Only an asset the caller may manage can be placed in a collection. Without
                // this, any member could build a gallery out of someone else's photographs.
                var asset = await _assets.GetSummaryAsync(MediaId.From(toAdd), cancellationToken);

                if (asset is null)
                {
                    return Result.Failure(MediaErrors.AssetNotFound);
                }

                var manageable = await _guard.CanManageAsync(asset, cancellationToken);

                if (manageable.IsFailure)
                {
                    return manageable;
                }

                var added = collection.Add(MediaId.From(toAdd), request.Caption);
                return added.IsFailure ? Result.Failure(added.Error) : Result.Success();

            case "remove":
                return request.MediaId is { } toRemove
                    ? collection.Remove(MediaId.From(toRemove))
                    : Result.Failure(MediaErrors.CollectionItemNotFound);

            case "reorder":
                return collection.Reorder(
                    request.MediaIds.Select(MediaId.From).ToList());

            case "promote":
                return request.MediaId is { } toPromote
                    ? collection.MakePrimary(MediaId.From(toPromote))
                    : Result.Failure(MediaErrors.CollectionItemNotFound);

            default:
                return Result.Failure(Error.Validation(
                    MediaErrorCodes.CollectionKindMismatch,
                    "That is not a collection operation."));
        }
    }
}
