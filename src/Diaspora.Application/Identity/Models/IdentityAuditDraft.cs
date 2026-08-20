using Diaspora.SharedKernel.Identity;

namespace Diaspora.Application.Identity.Models;

/// <summary>
/// One entry destined for the platform audit trail.
/// </summary>
public sealed record IdentityAuditDraft
{
    /// <summary>A value from <c>IdentityAuditActions</c>.</summary>
    public required string Action { get; init; }

    public required string EntityType { get; init; }

    public required string EntityId { get; init; }

    public UserId? SubjectUserId { get; init; }

    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
