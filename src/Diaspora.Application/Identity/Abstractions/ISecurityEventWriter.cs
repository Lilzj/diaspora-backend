using Diaspora.Application.Identity.Models;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Appends to the security event log, enriching each entry with the ambient request
/// context so call sites do not have to thread IP address and correlation id around.
/// </summary>
public interface ISecurityEventWriter
{
    Task WriteAsync(SecurityEventDraft draft, CancellationToken cancellationToken = default);
}
