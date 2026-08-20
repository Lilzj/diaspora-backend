using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Intelligence.V1.Events;

/// <summary>
/// Something happening now that a member may need to act on.
/// <para>
/// <b>Published <em>in addition to</em> <see cref="OfficialNoticePublishedIntegrationEvent"/>, never
/// instead of it.</b> A consumer subscribed to all official notices must not stop receiving the most
/// important ones because they qualified for a narrower contract. What this adds is a subscription a
/// consumer can make that is only ever the urgent things — which is what lets a deployment route
/// emergencies to a channel that ignores quiet hours without also waking people for fee schedules.
/// </para>
/// <para>
/// <b>Which notices qualify is an editorial decision, not a hard-coded list.</b> A notice raises this
/// when its category declares the emergency alert family, and a deployment adding "Cyclone Warnings"
/// sets that on the category. A list of keys inside this module would mean the deployment finds out
/// during a cyclone that its warnings are routine.
/// </para>
/// </summary>
public sealed record EmergencyAlertPublishedIntegrationEvent : IntegrationEvent
{
    public required Guid OfficialNoticeId { get; init; }

    public required string CountryCode { get; init; }

    public required string CategoryKey { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    /// <summary>PascalCase. Almost always <c>Critical</c> or <c>High</c>, and not guaranteed to be.</summary>
    public required string Severity { get; init; }

    public required Guid AuthorityId { get; init; }

    public required string AuthorityName { get; init; }

    /// <summary>The sub-national area, when the alert does not apply nationwide.</summary>
    public string? Region { get; init; }

    public DateTimeOffset? EffectiveFrom { get; init; }

    /// <summary>When it stops applying. A consumer holding a live banner needs this.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>The editor. Attribution, never a recipient.</summary>
    public Guid? PublishedBy { get; init; }
}

/// <summary>
/// A formal statement from government went live.
/// <para>
/// Published in addition to the general notice contract, on the same terms as the emergency one and
/// for the same reason: a consumer that wants only what the state itself said — a government-relations
/// digest, an official-only feed — can subscribe to this without filtering a category list it would
/// have to keep in step with a deployment's own.
/// </para>
/// </summary>
public sealed record GovernmentAnnouncementPublishedIntegrationEvent : IntegrationEvent
{
    public required Guid OfficialNoticeId { get; init; }

    public required string CountryCode { get; init; }

    public required string CategoryKey { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    /// <summary>PascalCase.</summary>
    public required string Severity { get; init; }

    public required Guid AuthorityId { get; init; }

    public required string AuthorityName { get; init; }

    /// <summary>PascalCase — <c>GovernmentMinistry</c>, <c>ImmigrationDepartment</c>.</summary>
    public required string AuthorityType { get; init; }

    /// <summary>The authority's own reference — a circular or gazette number.</summary>
    public string? ReferenceNumber { get; init; }

    public DateTimeOffset? EffectiveFrom { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>The editor. Attribution, never a recipient.</summary>
    public Guid? PublishedBy { get; init; }
}

/// <summary>
/// A mission in the embassy directory was recorded, edited, verified, archived or restored.
/// <para>
/// <b>Two country codes, and a consumer that ignores one of them will show the wrong list.</b>
/// <see cref="CountryCode"/> is where the mission physically is; <see cref="ServesCountryCode"/> is
/// whose nationals it serves. Neither is derivable from the other, and a mission belongs on both
/// countries' surfaces for different reasons.
/// </para>
/// </summary>
public sealed record EmbassyUpdatedIntegrationEvent : IntegrationEvent
{
    public required Guid EmbassyId { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2 — where the mission is located.</summary>
    public required string CountryCode { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2 — whose nationals it serves.</summary>
    public required string ServesCountryCode { get; init; }

    public required string Name { get; init; }

    /// <summary>One of <c>created</c>, <c>updated</c>, <c>verified</c>, <c>rejected</c>, <c>archived</c>, <c>restored</c>.</summary>
    public required string Change { get; init; }

    public required bool IsVerified { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }

    /// <summary>The editor. Attribution, never a recipient.</summary>
    public Guid? ChangedBy { get; init; }
}

/// <summary>
/// A visa or immigration advisory was written, published, edited, archived or restored.
/// <para>
/// <b><see cref="AppliesToCountryCode"/> is why this contract exists rather than a notice covering
/// it.</b> Visa rules are a relation between a destination and a nationality, and it is the only
/// content in this phase that is. B14 personalizing "rules that changed for people like you" needs
/// both halves; a consumer holding only the destination would show a Nigerian passport holder a
/// change that applies to EU nationals.
/// </para>
/// </summary>
public sealed record VisaPolicyUpdatedIntegrationEvent : IntegrationEvent
{
    public required Guid VisaAdvisoryId { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2 — the destination the rules govern entry to.</summary>
    public required string CountryCode { get; init; }

    /// <summary>The nationality the rules apply to, or null when they apply to everybody.</summary>
    public string? AppliesToCountryCode { get; init; }

    public required string Title { get; init; }

    /// <summary>The visa class — <c>Student</c>, <c>Skilled Worker</c>. Free text by design.</summary>
    public string? VisaCategory { get; init; }

    /// <summary>One of <c>created</c>, <c>updated</c>, <c>published</c>, <c>archived</c>, <c>restored</c>.</summary>
    public required string Change { get; init; }

    /// <summary>PascalCase — <c>Draft</c>, <c>Published</c>, <c>Archived</c>.</summary>
    public required string Status { get; init; }

    public required Guid AuthorityId { get; init; }

    public DateTimeOffset? EffectiveFrom { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }

    /// <summary>The editor. Attribution, never a recipient.</summary>
    public Guid? ChangedBy { get; init; }
}

/// <summary>
/// A public holiday or official closure was added to a country's calendar.
/// <para>
/// <b>The closure flags travel because they are the whole reason anybody consumes this.</b>
/// "Independence Day, 12 March" is a fact about a country; "the passport office is shut on 12 March"
/// is the fact that changes what a member does this week. A consumer that had to load the entry to
/// find out would be loading it for every holiday in the calendar.
/// </para>
/// </summary>
public sealed record PublicHolidayPublishedIntegrationEvent : IntegrationEvent
{
    public required Guid PublicHolidayId { get; init; }

    public required string CountryCode { get; init; }

    public required string Name { get; init; }

    /// <summary>PascalCase — <c>National</c>, <c>Religious</c>, <c>EmbassyClosure</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The day it falls on. Date-only in meaning; midnight UTC on the wire.</summary>
    public required DateTimeOffset ObservedOn { get; init; }

    /// <summary>The last day of a multi-day observance, or the same day as a single one.</summary>
    public required DateTimeOffset ObservedUntil { get; init; }

    /// <summary>One of <c>published</c>, <c>updated</c>, <c>archived</c>, <c>restored</c>.</summary>
    public required string Change { get; init; }

    public required bool ClosesGovernmentOffices { get; init; }

    public required bool ClosesEmbassies { get; init; }

    /// <summary>The area it applies to, or null for nationwide.</summary>
    public string? Region { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }

    /// <summary>The editor. Attribution, never a recipient.</summary>
    public Guid? ChangedBy { get; init; }
}

/// <summary>
/// A country's emergency information changed.
/// <para>
/// <b>A count and no numbers, and that is a safety decision rather than a modelling one.</b> A stale
/// <c>999</c> replayed from an outbox three days after a country changed it is a message telling
/// somebody to dial a number that no longer answers. Delivery is at-least-once and replay is normal,
/// so the only safe payload is "the set moved, go and read it".
/// </para>
/// </summary>
public sealed record EmergencyInformationUpdatedIntegrationEvent : IntegrationEvent
{
    public required string CountryCode { get; init; }

    /// <summary>One of <c>created</c>, <c>updated</c>.</summary>
    public required string Change { get; init; }

    /// <summary>How many contacts the country now lists. Never which.</summary>
    public required int ContactCount { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }

    /// <summary>The editor. Attribution, never a recipient.</summary>
    public Guid? ChangedBy { get; init; }
}

/// <summary>
/// An issuing authority was recorded, edited, verified, archived or restored.
/// <para>
/// <b>The verification transitions are what consumers subscribe for.</b> An authority becoming
/// verified is the moment every notice it has ever issued becomes publishable and every badge showing
/// its name becomes official — and a consumer that only heard about notices would refresh that badge
/// whenever a notice happened to be edited, which is not the same thing.
/// </para>
/// </summary>
public sealed record IssuingAuthorityUpdatedIntegrationEvent : IntegrationEvent
{
    public required Guid IssuingAuthorityId { get; init; }

    public required string CountryCode { get; init; }

    public required string Name { get; init; }

    /// <summary>PascalCase — <c>ImmigrationDepartment</c>, <c>Embassy</c>, <c>Other</c>.</summary>
    public required string AuthorityType { get; init; }

    /// <summary>One of <c>created</c>, <c>updated</c>, <c>verified</c>, <c>rejected</c>, <c>archived</c>, <c>restored</c>.</summary>
    public required string Change { get; init; }

    public required bool IsVerified { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }

    /// <summary>The editor. Attribution, never a recipient.</summary>
    public Guid? ChangedBy { get; init; }
}

/// <summary>
/// A notice category was created, renamed, archived or restored.
/// <para>
/// The vocabulary a member subscribes to, so a client caching a preference screen and a routing rule
/// keyed on a category both hear about it. The key travels rather than only the identifier, because a
/// consumer maintaining a preference list has no way to resolve one into the other.
/// </para>
/// </summary>
public sealed record NoticeCategoryUpdatedIntegrationEvent : IntegrationEvent
{
    public required Guid NoticeCategoryId { get; init; }

    /// <summary>The stable key. Lower-case kebab, unique, never rewritten.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    /// <summary>One of <c>created</c>, <c>updated</c>, <c>archived</c>, <c>restored</c>.</summary>
    public required string Change { get; init; }

    public required bool IsActive { get; init; }

    /// <summary>PascalCase — what a notice here is worth before an editor decides otherwise.</summary>
    public required string DefaultSeverity { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }

    /// <summary>The editor. Attribution, never a recipient.</summary>
    public Guid? ChangedBy { get; init; }
}
