using Diaspora.Domain.Identity.Security;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Domain.Identity.Repositories;

/// <summary>
/// Persistence contract for the append-only <see cref="SecurityEvent"/> log.
/// No update or delete member exists, by design.
/// </summary>
public interface ISecurityEventRepository
{
    Task AddAsync(SecurityEvent securityEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SecurityEvent>> ListByUserAsync(
        UserId userId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How many events of a type have been recorded for a member since a cut-off.
    /// Backs rate-limiting and repeat-abuse detection.
    /// </summary>
    Task<int> CountByTypeSinceAsync(
        UserId userId,
        SecurityEventType type,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);
}
