using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.Modules.Media.Domain.Uploads;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Uploads;

/// <summary>Moves a session between its open states, or ends it.</summary>
public sealed class SetUploadStateCommandHandler
    : ICommandHandler<SetUploadStateCommand, UploadSessionResponse>
{
    private readonly IUploadSessionRepository _sessions;
    private readonly UploadCoordinator _uploads;
    private readonly UploadMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public SetUploadStateCommandHandler(
        IUploadSessionRepository sessions,
        UploadCoordinator uploads,
        UploadMapper mapper,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _sessions = sessions;
        _uploads = uploads;
        _mapper = mapper;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UploadSessionResponse>> Handle(
        SetUploadStateCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<UploadSessionResponse>(MediaErrors.NotAuthenticated);
        }

        var session = await _sessions.GetAsync(
            UploadSessionId.From(request.SessionId), cancellationToken);

        if (session is null || session.StartedBy != userId)
        {
            return Result.Failure<UploadSessionResponse>(MediaErrors.UploadSessionNotFound);
        }

        var target = MediaWireNames.Parse<UploadSessionStatus>(
            request.State,
            Error.Validation(
                MediaErrorCodes.UploadSessionNotOpen, "That is not an upload state."));

        if (target.IsFailure)
        {
            return Result.Failure<UploadSessionResponse>(target.Error);
        }

        var applied = target.Value switch
        {
            UploadSessionStatus.Paused => session.Pause(),
            UploadSessionStatus.Uploading or UploadSessionStatus.Queued => session.Resume(),
            UploadSessionStatus.Cancelled => await _uploads.CancelAsync(session, cancellationToken),

            // Completed, Failed and Expired are outcomes the server reaches, not states a
            // client may assert. Accepting them would let a client mark its own upload
            // complete without the bytes having arrived.
            _ => Result.Failure(MediaErrors.UploadSessionNotOpen)
        };

        if (applied.IsFailure)
        {
            return Result.Failure<UploadSessionResponse>(applied.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(_mapper.ToResponse(session));
    }
}
