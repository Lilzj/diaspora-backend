using Diaspora.Application.Identity.Validation;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Sessions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Application.Identity.Mapping;

/// <summary>
/// Projects sessions for the member-facing session list. Refresh token hashes are never
/// part of the projection.
/// </summary>
public static class SessionMapper
{
    public static SessionResponse ToResponse(UserSession session, SessionId? currentSessionId)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new SessionResponse
        {
            Id = session.Id.Value,
            DeviceName = session.DeviceName,
            Platform = IdentityValueParser.Format(session.Platform),
            AppVersion = session.AppVersion,
            IpAddress = session.IpAddress,
            Status = session.Status.ToString(),
            CreatedAt = session.CreatedAt,
            LastSeenAt = session.LastSeenAt,
            ExpiresAt = session.AbsoluteExpiresAt,
            RevokedAt = session.RevokedAt,
            RevocationReason = session.RevocationReason?.ToString(),
            IsCurrent = currentSessionId is not null && session.Id == currentSessionId
        };
    }

    public static IReadOnlyList<SessionResponse> ToResponses(
        IEnumerable<UserSession> sessions,
        SessionId? currentSessionId) =>
        sessions.Select(session => ToResponse(session, currentSessionId)).ToArray();
}
