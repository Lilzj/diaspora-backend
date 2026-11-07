using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Services.Models;

namespace Diaspora.Infrastructure.Identity.Auditing;

/// <summary>
/// Adapts identity audit drafts onto the platform kernel's <c>IAuditService</c>.
/// <para>
/// Identity records who did what to whom; the platform owns where that goes. Keeping the
/// adapter here means the module never learns the audit sink's shape, and a change of sink
/// touches one class.
/// </para>
/// </summary>
public sealed class IdentityAuditWriter : IIdentityAuditWriter
{
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly IRequestContext _requestContext;

    public IdentityAuditWriter(
        IAuditService auditService,
        ICurrentUser currentUser,
        IRequestContext requestContext)
    {
        _auditService = auditService;
        _currentUser = currentUser;
        _requestContext = requestContext;
    }

    public Task WriteAsync(IdentityAuditDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var metadata = draft.Metadata is null
            ? null
            : new Dictionary<string, string>(draft.Metadata);

        if (draft.SubjectUserId is not null)
        {
            metadata ??= [];
            // The subject is recorded separately from the actor: an administrator
            // suspending a member produces one record naming both, and conflating them
            // would make the trail useless for exactly the cases it exists for.
            metadata["subjectUserId"] = draft.SubjectUserId.Value.ToString();
        }

        return _auditService.RecordAsync(
            new AuditEntry
            {
                Action = draft.Action,
                EntityType = draft.EntityType,
                EntityId = draft.EntityId,
                ActorId = _currentUser.UserId?.Value.ToString(),
                ActorRole = _currentUser.Role,
                Metadata = metadata,
                CorrelationId = _requestContext.CorrelationId
            },
            cancellationToken);
    }
}
