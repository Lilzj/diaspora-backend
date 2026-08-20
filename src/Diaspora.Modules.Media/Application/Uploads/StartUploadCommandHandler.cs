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

/// <summary>
/// Opens an upload after refusing everything decidable before a byte arrives.
/// <para>
/// Front-loading the refusals is the point. Rejecting a 600 MB video at initialisation costs
/// one round trip; rejecting it after the bytes have crossed a mobile network costs the member
/// their data allowance and costs the platform the bandwidth either way.
/// </para>
/// </summary>
public sealed class StartUploadCommandHandler
    : ICommandHandler<StartUploadCommand, UploadSessionResponse>
{
    private readonly UploadCoordinator _uploads;
    private readonly UploadMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public StartUploadCommandHandler(
        UploadCoordinator uploads,
        UploadMapper mapper,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _uploads = uploads;
        _mapper = mapper;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UploadSessionResponse>> Handle(
        StartUploadCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<UploadSessionResponse>(MediaErrors.NotAuthenticated);
        }

        var kind = MediaWireNames.Parse<MediaKind>(
            request.Request.Kind,
            Error.Validation(MediaErrorCodes.ContentTypeNotAllowed, "That is not a media kind."));

        if (kind.IsFailure)
        {
            return Result.Failure<UploadSessionResponse>(kind.Error);
        }

        var contentType = MediaContentType.TryCreate(request.Request.ContentType);

        if (contentType.IsFailure)
        {
            return Result.Failure<UploadSessionResponse>(contentType.Error);
        }

        ContentHash? declaredHash = null;

        if (!string.IsNullOrWhiteSpace(request.Request.ContentHash))
        {
            var hash = ContentHash.TryCreate(request.Request.ContentHash);

            if (hash.IsFailure)
            {
                return Result.Failure<UploadSessionResponse>(hash.Error);
            }

            declaredHash = hash.Value;
        }

        var owner = ResolveOwner(request.Request.OwnerType, request.Request.OwnerId);

        if (owner.IsFailure)
        {
            return Result.Failure<UploadSessionResponse>(owner.Error);
        }

        var session = await _uploads.StartAsync(
            userId,
            kind.Value,
            contentType.Value,
            SafeFileName.Create(request.Request.FileName),
            ByteSize.FromBytes(request.Request.SizeBytes),
            declaredHash,
            owner.Value,
            request.Request.Role,
            cancellationToken);

        if (session.IsFailure)
        {
            return Result.Failure<UploadSessionResponse>(session.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(_mapper.ToResponse(session.Value));
    }

    /// <summary>
    /// An owner is optional, but a half-specified one is not: a type without an identifier, or
    /// an identifier without a type, is a caller who thinks they attached the upload to
    /// something and has not.
    /// </summary>
    internal static Result<MediaOwner> ResolveOwner(string? ownerType, Guid? ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerType) && ownerId is null)
        {
            return Result.Success(MediaOwner.Unassigned);
        }

        if (string.IsNullOrWhiteSpace(ownerType) || ownerId is null)
        {
            return Result.Failure<MediaOwner>(MediaErrors.ReferenceOwnerInvalid);
        }

        var type = MediaWireNames.Parse<MediaOwnerType>(
            ownerType,
            Error.Validation(MediaErrorCodes.ReferenceOwnerInvalid, "That is not an owner type."));

        return type.IsFailure
            ? Result.Failure<MediaOwner>(type.Error)
            : MediaOwner.TryCreate(type.Value, ownerId.Value);
    }
}
