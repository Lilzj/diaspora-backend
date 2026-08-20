using System.Text.Json;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Security;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Infrastructure.Identity.Security;

/// <summary>
/// Appends security events, enriched with the ambient request context.
/// <para>
/// Call sites supply only what they know — who, what, how bad. IP address, user agent and
/// correlation id are attached here, which is both less error-prone and the reason those
/// fields are populated consistently across every event type.
/// </para>
/// <para>
/// Events are added to the unit of work, not written immediately: a security event
/// describing a change that then rolled back would be worse than no event at all.
/// </para>
/// </summary>
public sealed class SecurityEventWriter : ISecurityEventWriter
{
    private static readonly JsonSerializerOptions MetadataSerializerOptions = new()
    {
        WriteIndented = false
    };

    private readonly ISecurityEventRepository _repository;
    private readonly IRequestContext _requestContext;
    private readonly IClock _clock;
    private readonly ILogger<SecurityEventWriter> _logger;

    public SecurityEventWriter(
        ISecurityEventRepository repository,
        IRequestContext requestContext,
        IClock clock,
        ILogger<SecurityEventWriter> logger)
    {
        _repository = repository;
        _requestContext = requestContext;
        _clock = clock;
        _logger = logger;
    }

    public async Task WriteAsync(SecurityEventDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var securityEvent = SecurityEvent.Record(
            draft.UserId,
            draft.Type,
            draft.Severity,
            _clock.UtcNow,
            draft.Description,
            draft.SessionId,
            _requestContext.IpAddress,
            _requestContext.UserAgent,
            _requestContext.DeviceId,
            _requestContext.CorrelationId,
            draft.Metadata is null ? null : JsonSerializer.Serialize(draft.Metadata, MetadataSerializerOptions));

        await _repository.AddAsync(securityEvent, cancellationToken);

        if (draft.Severity >= SecurityEventSeverity.Warning)
        {
            _logger.LogWarning(
                "Security event {Type} ({Severity}) for user {UserId}: {Description}",
                draft.Type, draft.Severity, draft.UserId, draft.Description);
        }
    }
}
