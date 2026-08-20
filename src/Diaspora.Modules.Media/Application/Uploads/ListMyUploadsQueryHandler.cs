using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Domain.Uploads;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Uploads;

/// <summary>Lists the caller's open uploads, newest first.</summary>
public sealed class ListMyUploadsQueryHandler
    : IQueryHandler<ListMyUploadsQuery, IReadOnlyList<UploadSessionResponse>>
{
    private readonly IUploadSessionRepository _sessions;
    private readonly UploadMapper _mapper;
    private readonly ICurrentUser _currentUser;

    public ListMyUploadsQueryHandler(
        IUploadSessionRepository sessions, UploadMapper mapper, ICurrentUser currentUser)
    {
        _sessions = sessions;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<UploadSessionResponse>>> Handle(
        ListMyUploadsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<IReadOnlyList<UploadSessionResponse>>(
                MediaErrors.NotAuthenticated);
        }

        var sessions = await _sessions.ListOpenForUserAsync(userId, cancellationToken);

        IReadOnlyList<UploadSessionResponse> responses = sessions
            .Select(session => _mapper.ToResponse(session))
            .ToList();

        return Result.Success(responses);
    }
}
