using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>
/// Raised when a member reports content.
/// <para>
/// Carries the running open-report count, so a consumer that only learns a report happened need
/// not go and count them itself. Since 7 Nov 2026 the report also goes to Trust as a moderation
/// case, and Trust, not this module, decides what many reports mean.
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

    /// <summary>What the reported content is called, for the moderation case. Never the reporter.</summary>
    public string? ContentTitle { get; init; }
}
