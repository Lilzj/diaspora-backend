using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when a member reports a job advert.
/// <para>
/// <b>It deliberately carries no reporter identity.</b> Whatever subscribes to the bus receives
/// the contract that follows from this, and somebody who reported a fraudulent employer must
/// never become identifiable to them — they may be applying for other jobs in the same small
/// industry, in a country where they have no standing yet. The reporter is on the report row,
/// behind moderator authorisation, and nowhere else.
/// </para>
/// </summary>
public sealed record JobPostingReportedDomainEvent : DomainEvent
{
    public required JobReportId ReportId { get; init; }

    public required JobPostingId PostingId { get; init; }

    public required EmployerProfileId EmployerId { get; init; }

    public required JobReportReason Reason { get; init; }

    /// <summary>How many distinct members have now reported this posting.</summary>
    public required int TotalReports { get; init; }

    public required DateTimeOffset ReportedAt { get; init; }
}
