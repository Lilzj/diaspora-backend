using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Auditing;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Events;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Identity.EventHandlers;

/// <summary>
/// Escalates a refresh-token replay.
/// <para>
/// The session is already dead by the time this runs — the aggregate revokes it before
/// the failure is returned. What is left is making sure a human finds out: this is the
/// strongest evidence of credential theft the system produces, and it should never be
/// buried in an ordinary audit row.
/// </para>
/// </summary>
public sealed class RefreshTokenReuseDetectedEventHandler : DomainEventHandler<RefreshTokenReuseDetectedDomainEvent>
{
    private readonly IIdentityAuditWriter _audit;
    private readonly ILogger<RefreshTokenReuseDetectedEventHandler> _logger;

    public RefreshTokenReuseDetectedEventHandler(
        IIdentityAuditWriter audit,
        ILogger<RefreshTokenReuseDetectedEventHandler> logger)
    {
        _audit = audit;
        _logger = logger;
    }

    protected override Task HandleAsync(
        RefreshTokenReuseDetectedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            "SECURITY: refresh token replay on session {SessionId} for user {UserId}. " +
            "Token {TokenId} had already been consumed; the session was revoked.",
            domainEvent.SessionId, domainEvent.UserId, domainEvent.ReplayedTokenId);

        return _audit.WriteAsync(new IdentityAuditDraft
        {
            Action = IdentityAuditActions.SessionRevoked,
            EntityType = IdentityAuditActions.EntityTypes.Session,
            EntityId = domainEvent.SessionId.Value.ToString(),
            SubjectUserId = domainEvent.UserId,
            Metadata = new Dictionary<string, string>
            {
                ["reason"] = "TokenReuseDetected",
                ["replayedTokenId"] = domainEvent.ReplayedTokenId.ToString()
            }
        }, cancellationToken);
    }
}
