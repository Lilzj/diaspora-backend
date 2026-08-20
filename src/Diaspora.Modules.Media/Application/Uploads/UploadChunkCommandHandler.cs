using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.Modules.Media.Domain.Uploads;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Uploads;

/// <summary>Receives one part.</summary>
public sealed class UploadChunkCommandHandler
    : ICommandHandler<UploadChunkCommand, UploadSessionResponse>
{
    private readonly IUploadSessionRepository _sessions;
    private readonly UploadCoordinator _uploads;
    private readonly UploadMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public UploadChunkCommandHandler(
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
        UploadChunkCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<UploadSessionResponse>(MediaErrors.NotAuthenticated);
        }

        var session = await _sessions.GetAsync(
            UploadSessionId.From(request.SessionId), cancellationToken);

        // A session belonging to someone else is reported as not-found rather than forbidden.
        // Distinguishing the two would confirm that a session identifier exists, which is a
        // free oracle for anyone enumerating them.
        if (session is null || session.StartedBy != userId)
        {
            return Result.Failure<UploadSessionResponse>(MediaErrors.UploadSessionNotFound);
        }

        var received = await _uploads.ReceiveChunkAsync(
            session,
            request.ChunkNumber,
            request.Content,
            ParseHash(request.ChunkHash),
            cancellationToken);

        // Saved either way: a session the domain moved to Failed must be persisted in that
        // state, or the client retries against a session that still looks open.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return received.IsFailure
            ? Result.Failure<UploadSessionResponse>(received.Error)
            : Result.Success(_mapper.ToResponse(session));
    }

    private static ContentHash? ParseHash(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var hash = ContentHash.TryCreate(value);
        return hash.IsSuccess ? hash.Value : null;
    }
}
