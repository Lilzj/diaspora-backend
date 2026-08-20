using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Uploads;

/// <summary>Stores a small file and returns the asset it became.</summary>
public sealed class DirectUploadCommandHandler
    : ICommandHandler<DirectUploadCommand, MediaAssetResponse>
{
    private readonly UploadCoordinator _uploads;
    private readonly MediaMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public DirectUploadCommandHandler(
        UploadCoordinator uploads,
        MediaMapper mapper,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _uploads = uploads;
        _mapper = mapper;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<MediaAssetResponse>> Handle(
        DirectUploadCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<MediaAssetResponse>(MediaErrors.NotAuthenticated);
        }

        var kind = MediaWireNames.Parse<MediaKind>(
            request.Kind,
            Error.Validation(MediaErrorCodes.ContentTypeNotAllowed, "That is not a media kind."));

        if (kind.IsFailure)
        {
            return Result.Failure<MediaAssetResponse>(kind.Error);
        }

        var contentType = MediaContentType.TryCreate(request.ContentType);

        if (contentType.IsFailure)
        {
            return Result.Failure<MediaAssetResponse>(contentType.Error);
        }

        var owner = StartUploadCommandHandler.ResolveOwner(request.OwnerType, request.OwnerId);

        if (owner.IsFailure)
        {
            return Result.Failure<MediaAssetResponse>(owner.Error);
        }

        var asset = await _uploads.UploadDirectAsync(
            userId,
            kind.Value,
            contentType.Value,
            SafeFileName.Create(request.FileName),
            request.Content,
            owner.Value,
            request.Role,
            cancellationToken);

        if (asset.IsFailure)
        {
            return Result.Failure<MediaAssetResponse>(asset.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(_mapper.ToResponse(asset.Value));
    }
}
