using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>A moderator upheld a report against an event or an organisation (REP-13).</summary>
public sealed record CommunityLifeReportUpheldDomainEvent : DomainEvent
{
    public required CommunityLifeReportId ReportId { get; init; }

    public required ReportSubjectType SubjectType { get; init; }

    public required Guid SubjectId { get; init; }

    public required UserId ReportedByUserId { get; init; }

    public required DateTimeOffset UpheldAt { get; init; }
}
