using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Content.V1.Events;

/// <summary>
/// A member's report was upheld by a moderator (REP-13). Published by every module that takes
/// reports — Community, CommunityLife, Housing, Jobs and Marketplace — so Trust pays the reporter
/// from one contract and a sixth reporting module needs no Trust change.
/// <para>
/// <b>Who reported is the point of it.</b> The moderator's note and the report's reason stay in
/// the module: Trust needs the reporter, the content, and whose content it was, nothing else.
/// </para>
/// </summary>
public sealed record ContentReportUpheldIntegrationEvent : IntegrationEvent
{
    public required Guid ReportId { get; init; }

    /// <summary>
    /// The module that decided it: <c>community</c>, <c>community-life</c>, <c>housing</c>,
    /// <c>jobs</c> or <c>marketplace</c>. Report ids are unique within a module, not across them.
    /// </summary>
    public required string Module { get; init; }

    /// <summary>What was reported, in the module's own words: <c>post</c>, <c>event</c>, <c>listing</c>.</summary>
    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required Guid ReporterUserId { get; init; }

    /// <summary>Whose content it was, when the module knows. A member reporting their own is not paid.</summary>
    public Guid? ContentOwnerUserId { get; init; }

    public required DateTimeOffset UpheldAt { get; init; }
}
