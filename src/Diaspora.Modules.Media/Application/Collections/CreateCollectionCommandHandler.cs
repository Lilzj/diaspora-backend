using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Application.Uploads;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Collections;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Collections;

/// <summary>
/// Creates a collection, or returns the existing one for that owner and kind.
/// <para>
/// Idempotent by owner and kind, because that is how a vertical actually calls it: Housing
/// asks for <em>the</em> gallery of listing 7f3c…, and a second call after a retry must not
/// produce a second gallery. The unique index enforces the same thing at the database level.
/// </para>
/// </summary>
public sealed class CreateCollectionCommandHandler
    : ICommandHandler<CreateCollectionCommand, MediaCollectionResponse>
{
    private readonly IMediaCollectionRepository _collections;
    private readonly IMediaAssetRepository _assets;
    private readonly CollectionMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCollectionCommandHandler(
        IMediaCollectionRepository collections,
        IMediaAssetRepository assets,
        CollectionMapper mapper,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _collections = collections;
        _assets = assets;
        _mapper = mapper;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<MediaCollectionResponse>> Handle(
        CreateCollectionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<MediaCollectionResponse>(MediaErrors.NotAuthenticated);
        }

        var kind = MediaWireNames.Parse<MediaCollectionKind>(
            request.Request.Kind,
            Error.Validation(
                MediaErrorCodes.CollectionKindMismatch, "That is not a collection kind."));

        if (kind.IsFailure)
        {
            return Result.Failure<MediaCollectionResponse>(kind.Error);
        }

        var owner = StartUploadCommandHandler.ResolveOwner(
            request.Request.OwnerType, request.Request.OwnerId);

        if (owner.IsFailure)
        {
            return Result.Failure<MediaCollectionResponse>(owner.Error);
        }

        var collection = await _collections.GetForOwnerAsync(
            owner.Value, kind.Value, cancellationToken);

        if (collection is null)
        {
            var created = MediaCollection.Create(
                kind.Value, owner.Value, userId, request.Request.Name);

            if (created.IsFailure)
            {
                return Result.Failure<MediaCollectionResponse>(created.Error);
            }

            collection = created.Value;
            await _collections.AddAsync(collection, cancellationToken);
        }

        foreach (var mediaId in request.Request.MediaIds)
        {
            var added = collection.Add(MediaId.From(mediaId));

            if (added.IsFailure)
            {
                return Result.Failure<MediaCollectionResponse>(added.Error);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await ProjectAsync(collection, cancellationToken));
    }

    private async Task<MediaCollectionResponse> ProjectAsync(
        MediaCollection collection, CancellationToken cancellationToken)
    {
        var ids = collection.Items.Select(item => item.MediaId).ToList();
        var assets = await _assets.GetManyAsync(ids, cancellationToken);

        return _mapper.ToResponse(collection, assets.ToDictionary(asset => asset.Id));
    }
}
