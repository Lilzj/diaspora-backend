using Diaspora.Domain.Identity.Security;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Application.Identity.Models;

/// <summary>
/// A security event before ambient request context is attached. The writer fills in IP
/// address, user agent and correlation id.
/// </summary>
public sealed record SecurityEventDraft
{
    public UserId? UserId { get; init; }

    public required SecurityEventType Type { get; init; }

    public required SecurityEventSeverity Severity { get; init; }

    public string? Description { get; init; }

    public SessionId? SessionId { get; init; }

    /// <summary>Extra detail. Must never carry passwords, codes or tokens.</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
