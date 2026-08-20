using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>
/// Raised when a member reports an event or an organization.
/// <para>
/// <b>Carries no reporter identity, and that is not an oversight.</b> A report is filed by somebody
/// who may be in the same community as the person they reported. The moderator sees who filed it;
/// the bus does not, and neither does any consumer downstream.
/// </para>
/// </summary>
public sealed record CommunityLifeReportedDomainEvent : DomainEvent
{
    public required CommunityLifeReportId ReportId { get; init; }

    public required ReportSubjectType SubjectType { get; init; }

    public required Guid SubjectId { get; init; }

    public required CommunityLifeReportReason Reason { get; init; }

    /// <summary>How many distinct members have now reported it. What drives the auto-flag.</summary>
    public required int TotalReports { get; init; }

    public required DateTimeOffset ReportedAt { get; init; }
}
