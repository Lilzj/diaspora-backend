using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.Modules.Media.Domain.Uploads;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Uploads;

/// <summary>Assembles the parts, records the asset, queues the processing.</summary>
public sealed class CompleteUploadCommandHandler
    : ICommandHandler<CompleteUploadCommand, UploadSessionResponse>
{
    private readonly IUploadSessionRepository _sessions;
    private readonly UploadCoordinator _uploads;
    private readonly UploadMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteUploadCommandHandler(
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
        CompleteUploadCommand request, CancellationToken cancellationToken)
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

        var asset = await _uploads.CompleteAsync(session, cancellationToken);

        if (asset.IsFailure)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<UploadSessionResponse>(asset.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(_mapper.ToResponse(session, asset.Value));
    }
}
