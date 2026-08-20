using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Mapping;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Queries.GetSessions;

public sealed class GetSessionsQueryHandler : IQueryHandler<GetSessionsQuery, IReadOnlyList<SessionResponse>>
{
    private readonly IUserSessionRepository _sessions;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly IClock _clock;

    public GetSessionsQueryHandler(
        IUserSessionRepository sessions,
        ICurrentIdentityContext currentIdentity,
        IClock clock)
    {
        _sessions = sessions;
        _currentIdentity = currentIdentity;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<SessionResponse>>> Handle(
        GetSessionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentIdentity.UserId;
        if (userId is null)
        {
            return Result.Failure<IReadOnlyList<SessionResponse>>(UserErrors.InvalidCredentials);
        }

        var sessions = request.IncludeRevoked
            ? await _sessions.ListByUserAsync(userId, activeOnly: false, cancellationToken)
            : await _sessions.ListActiveByUserAsync(userId, _clock.UtcNow, cancellationToken);

        var responses = SessionMapper.ToResponses(
            sessions.OrderByDescending(session => session.LastSeenAt),
            _currentIdentity.SessionId);

        return Result.Success(responses);
    }
}
