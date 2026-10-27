using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>A moderator upheld a report against a job posting (REP-13).</summary>
public sealed record JobReportUpheldDomainEvent : DomainEvent
{
    public required JobReportId ReportId { get; init; }

    public required JobPostingId PostingId { get; init; }

    public required EmployerProfileId EmployerId { get; init; }

    public required UserId ReportedBy { get; init; }

    public required DateTimeOffset UpheldAt { get; init; }
}
