using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Intelligence.V1.Events;

/// <summary>
/// An official notice went live.
/// <para>
/// <b>The contract the Communication Platform routes on and the Recommendation Platform
/// personalizes from, and it carries everything either needs to decide without calling back.</b>
/// Country, category, severity and the issuing authority's name are what a routing rule matches and
/// what a notification template renders; a consumer forced to resolve any of them would turn every
/// published notice into a fan-out of reads against the Guides module.
/// </para>
/// <para>
/// <b>There is deliberately no recipient on this contract.</b> B13.1 established that every recipient
/// identity on a published contract is a nullable <c>Guid?</c> — and an official notice has no single
/// recipient at all. Its audience is "people who care about this country", which only B13 can resolve
/// because only B13 owns preferences, quiet hours and channel routing. Inventing a recipient field
/// here would be the <c>Guid.Empty</c> defect that phase records, with an audience instead of a
/// person.
/// </para>
/// <para>
/// <b>Every published notice raises this, including the ones that also raise an emergency or
/// government contract.</b> A consumer that subscribed to "all official notices" must not silently
/// stop receiving the most important ones because they qualified for a second, narrower contract.
/// </para>
/// </summary>
public sealed record OfficialNoticePublishedIntegrationEvent : IntegrationEvent
{
    public required Guid OfficialNoticeId { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2. Always present — a notice is about a country.</summary>
    public required string CountryCode { get; init; }

    /// <summary>The notice category key. What a routing rule and a preference both match on.</summary>
    public required string CategoryKey { get; init; }

    public required string Title { get; init; }

    /// <summary>The card text. What a notification renders without loading anything.</summary>
    public required string Summary { get; init; }

    /// <summary>PascalCase — <c>Critical</c>, <c>High</c>, <c>Medium</c>, <c>Low</c>, <c>Informational</c>.</summary>
    public required string Severity { get; init; }

    /// <summary>The issuing body's identifier.</summary>
    public required Guid AuthorityId { get; init; }

    /// <summary>The issuing body's name, so a template need not resolve it.</summary>
    public required string AuthorityName { get; init; }

    /// <summary>Whether Trust has confirmed the issuing body. Always true on a published notice.</summary>
    public required bool IsOfficial { get; init; }

    /// <summary>The sub-national area, when the notice does not apply nationwide.</summary>
    public string? Region { get; init; }

    /// <summary>When the authority issued it, which is not when this platform published it.</summary>
    public DateTimeOffset? PublicationDate { get; init; }

    public DateTimeOffset? EffectiveFrom { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>The editor who published it. Attribution, never a recipient.</summary>
    public Guid? PublishedBy { get; init; }
}

/// <summary>
/// An official notice changed after it was published, or moved through its editorial workflow.
/// <para>
/// <b>Separate from publication because consumers want different halves of it.</b> B13 cares about
/// publication and supersession — those are the moments a member should be told something. A search
/// index or a cache cares about every change, including a translation being added to a live notice.
/// One contract would make the notification platform subscribe to editorial noise.
/// </para>
/// <para>
/// <see cref="Change"/> is one of <c>created</c>, <c>updated</c>, <c>submitted</c>,
/// <c>verified</c>, <c>rejected</c>, <c>restored</c> or <c>expired</c>. Publication, supersession
/// and archival have contracts of their own.
/// </para>
/// </summary>
public sealed record OfficialNoticeUpdatedIntegrationEvent : IntegrationEvent
{
    public required Guid OfficialNoticeId { get; init; }

    public required string CountryCode { get; init; }

    public required string CategoryKey { get; init; }

    public required string Title { get; init; }

    /// <summary>One of the editorial verbs. Never <c>published</c> — that has its own contract.</summary>
    public required string Change { get; init; }

    /// <summary>PascalCase — <c>Draft</c>, <c>Submitted</c>, <c>Verified</c>, <c>Published</c>.</summary>
    public required string Status { get; init; }

    /// <summary>PascalCase.</summary>
    public required string Severity { get; init; }

    public required Guid AuthorityId { get; init; }

    public DateTimeOffset? EffectiveFrom { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }

    /// <summary>The editor. Attribution, never a recipient.</summary>
    public Guid? ChangedBy { get; init; }
}

/// <summary>
/// An official notice was replaced by a later one.
/// <para>
/// <b><see cref="SupersededByNoticeId"/> is the whole reason this contract exists separately.</b> A
/// consumer holding a message that links the old notice — a notification already delivered, a cached
/// card, a recommendation slot — needs somewhere to send whoever taps it. "This is no longer current"
/// without "here is what is" is the worst possible answer to give somebody who acted on the original.
/// </para>
/// </summary>
public sealed record OfficialNoticeSupersededIntegrationEvent : IntegrationEvent
{
    public required Guid OfficialNoticeId { get; init; }

    /// <summary>What replaced it. Published, and never the notice itself.</summary>
    public required Guid SupersededByNoticeId { get; init; }

    public required string CountryCode { get; init; }

    public required string CategoryKey { get; init; }

    public required string Title { get; init; }

    /// <summary>PascalCase. Carried so a consumer can decide whether to tell anybody.</summary>
    public required string Severity { get; init; }

    public required Guid AuthorityId { get; init; }

    public required DateTimeOffset SupersededAt { get; init; }

    /// <summary>The editor. Attribution, never a recipient.</summary>
    public Guid? ChangedBy { get; init; }
}

/// <summary>
/// An official notice was withdrawn.
/// <para>
/// <b>Distinct from supersession, and consumers must treat it differently.</b> A superseded notice
/// points somewhere; an archived one does not, because it was withdrawn rather than replaced —
/// published in error, or about something that turned out not to be happening. A consumer holding a
/// card for it should drop the card rather than redirect it.
/// </para>
/// </summary>
public sealed record OfficialNoticeArchivedIntegrationEvent : IntegrationEvent
{
    public required Guid OfficialNoticeId { get; init; }

    public required string CountryCode { get; init; }

    public required string CategoryKey { get; init; }

    public required string Title { get; init; }

    public required DateTimeOffset ArchivedAt { get; init; }

    /// <summary>The editor. Attribution, never a recipient.</summary>
    public Guid? ChangedBy { get; init; }
}
