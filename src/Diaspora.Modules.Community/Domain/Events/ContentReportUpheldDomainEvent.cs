using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>A moderator upheld a report. Trust pays the reporter for it (REP-13).</summary>
public sealed record ContentReportUpheldDomainEvent : DomainEvent
{
    public required ContentReportId ReportId { get; init; }

    public required ContentReference Content { get; init; }

    public required UserId ReporterId { get; init; }

    public Guid? ContentAuthorId { get; init; }

    public required DateTimeOffset UpheldAt { get; init; }
}
