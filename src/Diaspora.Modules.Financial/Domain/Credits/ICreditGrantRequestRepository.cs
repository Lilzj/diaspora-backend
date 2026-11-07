using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Credits;

/// <summary>Credit grants waiting for, or decided by, a second Admin (7 Nov 2026).</summary>
public interface ICreditGrantRequestRepository
{
    void Add(CreditGrantRequest request);

    void Update(CreditGrantRequest request);

    Task<CreditGrantRequest?> FindAsync(
        CreditGrantRequestId requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests newest first, paged like every other read here: <paramref name="before"/> is
    /// inclusive and <paramref name="excludeIds"/> are the ones already shown at that instant.
    /// Null <paramref name="status"/> lists every request.
    /// </summary>
    Task<IReadOnlyList<CreditGrantRequest>> ListAsync(
        CreditGrantRequestStatus? status,
        DateTimeOffset? before,
        IReadOnlyList<Guid> excludeIds,
        int take,
        CancellationToken cancellationToken = default);
}
