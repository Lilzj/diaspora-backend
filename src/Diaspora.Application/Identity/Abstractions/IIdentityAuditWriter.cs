using Diaspora.Application.Identity.Models;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Writes identity actions to the platform audit trail (B1's <c>IAuditService</c>),
/// with the vocabulary and redaction rules identity needs.
/// </summary>
public interface IIdentityAuditWriter
{
    Task WriteAsync(IdentityAuditDraft draft, CancellationToken cancellationToken = default);
}
