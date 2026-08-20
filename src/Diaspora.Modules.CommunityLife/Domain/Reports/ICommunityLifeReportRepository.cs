using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.CommunityLife.Domain.Reports;

/// <summary>How the module reads and writes reports.</summary>
public interface ICommunityLifeReportRepository
{
    Task<CommunityLifeReport?> GetAsync(
        CommunityLifeReportId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the member has already reported this subject.
    /// <para>
    /// The de-duplication check. Without it, one determined member could flag anything by reporting
    /// it five times, which is exactly the abuse an auto-flag threshold invites.
    /// </para>
    /// </summary>
    Task<bool> HasReportedAsync(
        ReportSubjectType subjectType,
        Guid subjectId,
        UserId reportedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>How many distinct members have reported the subject.</summary>
    Task<int> CountForSubjectAsync(
        ReportSubjectType subjectType,
        Guid subjectId,
        CancellationToken cancellationToken = default);

    /// <summary>The moderation queue, oldest unresolved first.</summary>
    Task<CursorPagedResult<CommunityLifeReport>> SearchAsync(
        ReportSubjectType? subjectType,
        IReadOnlyList<CommunityLifeReportStatus>? statuses,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    Task AddAsync(CommunityLifeReport report, CancellationToken cancellationToken = default);

    void Update(CommunityLifeReport report);
}
