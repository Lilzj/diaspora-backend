using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Services.Models;
using Microsoft.Extensions.Logging;

namespace Diaspora.Infrastructure.Services;

public sealed class DefaultAuditService : IAuditService
{
    private readonly ILogger<DefaultAuditService> _logger;

    public DefaultAuditService(ILogger<DefaultAuditService> logger)
    {
        _logger = logger;
    }

    public Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Audit: {Action} on {EntityType}:{EntityId} by {ActorId}",
            entry.Action, entry.EntityType, entry.EntityId, entry.ActorId ?? "System");
        return Task.CompletedTask;
    }
}
