using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.Modules.Media.Domain.Uploads;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Uploads;

/// <summary>Reads one of the caller's own upload sessions.</summary>
public sealed class GetUploadSessionQueryHandler
    : IQueryHandler<GetUploadSessionQuery, UploadSessionResponse>
{
    private readonly IUploadSessionRepository _sessions;
    private readonly IMediaAssetRepository _assets;
    private readonly UploadMapper _mapper;
    private readonly ICurrentUser _currentUser;

    public GetUploadSessionQueryHandler(
        IUploadSessionRepository sessions,
        IMediaAssetRepository assets,
        UploadMapper mapper,
        ICurrentUser currentUser)
    {
        _sessions = sessions;
        _assets = assets;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<Result<UploadSessionResponse>> Handle(
        GetUploadSessionQuery request, CancellationToken cancellationToken)
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

        var asset = session.MediaId is null
            ? null
            : await _assets.GetAsync(session.MediaId, cancellationToken);

        return Result.Success(_mapper.ToResponse(session, asset));
    }
}
