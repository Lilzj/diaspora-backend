using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>
/// Raised when a member reports content.
/// <para>
/// Carries the running open-report count, because the count is the thing anybody downstream
/// actually acts on: one report is a queue item, ten is an auto-hide, and a consumer that only
/// learns a report happened has to go and count them itself.
/// </para>
/// </summary>
public sealed record ContentReportedDomainEvent : DomainEvent
{
    public required ContentReportId ReportId { get; init; }

    public required ContentReference Content { get; init; }

    public required UserId ReporterId { get; init; }

    public required ReportReasonCode Reason { get; init; }

    public required int OpenReportCount { get; init; }

    public Guid? ContentAuthorId { get; init; }
}
