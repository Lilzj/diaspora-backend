using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Moderation.V1.Events;

/// <summary>
/// A member reported something (7 Nov 2026). Every module that takes reports publishes this, and
/// Trust turns it into a moderation case, or adds it to the open one for the same content.
/// <para>
/// Before this, reports went to five different module tables and Trust's queue was never fed.
/// </para>
/// </summary>
public sealed record ContentReportFiledIntegrationEvent : IntegrationEvent
{
    /// <summary>The module that took the report: "jobs", "housing", "community" and so on.</summary>
    public required string Module { get; init; }

    /// <summary>One of <see cref="ModerationContentTypes"/>.</summary>
    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required Guid ContentOwnerUserId { get; init; }

    public required Guid ReporterUserId { get; init; }

    /// <summary>A <see cref="ModerationReasonCode"/> name, mapped from the module's own reason.</summary>
    public required string ReasonCode { get; init; }

    /// <summary>The module's own reason, as the reporter chose it.</summary>
    public required string ModuleReason { get; init; }

    public string? Detail { get; init; }

    /// <summary>The module's report row, so a decision can resolve it.</summary>
    public required Guid ModuleReportId { get; init; }

    public string? ContentTitle { get; init; }

    public required DateTimeOffset FiledAt { get; init; }
}
