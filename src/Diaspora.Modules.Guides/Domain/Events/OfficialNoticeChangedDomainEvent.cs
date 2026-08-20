using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// An official notice moved — created, edited, submitted, verified, published, superseded, archived
/// or restored.
/// <para>
/// One event for the whole lifecycle, distinguished by <see cref="Change"/>. The relay is the single
/// place that decides which published contract each verb becomes, and that concentration is what
/// lets publication fan out to three different contracts — a general one, an emergency alert and a
/// government announcement — without three subscriptions racing each other.
/// </para>
/// <para>
/// <b>It carries the facts a consumer needs to decide without loading the notice.</b> Country,
/// category and severity are what B13 routes on and what B14 personalizes against, and a consumer
/// forced to call back for them would make every published notice a fan-out of reads against this
/// module.
/// </para>
/// </summary>
public sealed record OfficialNoticeChangedDomainEvent : DomainEvent
{
    public required OfficialNoticeId OfficialNoticeId { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2. Always present — a notice is about a country.</summary>
    public required string CountryCode { get; init; }

    /// <summary>The notice category key. What a reader subscribed to and what B13 routes on.</summary>
    public required string CategoryKey { get; init; }

    public required string Title { get; init; }

    /// <summary>
    /// The card text.
    /// <para>
    /// <b>Carried on the event so the relay never has to invent it.</b> Every publication contract
    /// requires a summary — it is what a notification renders — and the relay has three ways to get
    /// one: the aggregate stamps it, the handler passes it, or the relay reads the notice back. B13.1
    /// records what happens when a publisher does none of those and writes a placeholder into a
    /// required field: the messages go out, the consumers parse them, and every one of them is empty,
    /// for a whole phase, with no error anywhere. Stamping it here is the free route.
    /// </para>
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>One of the verbs in <see cref="IntelligenceChangeKinds"/>.</summary>
    public required string Change { get; init; }

    public required NoticeStatus Status { get; init; }

    public required NoticeSeverity Severity { get; init; }

    /// <summary>The issuing authority's identifier, resolved to a name by the relay.</summary>
    public required IssuingAuthorityId AuthorityId { get; init; }

    /// <summary>When the authority issued it, which is not when this platform published it.</summary>
    public DateTimeOffset? PublicationDate { get; init; }

    public DateTimeOffset? EffectiveFrom { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>The notice that replaced this one, on a supersede and nowhere else.</summary>
    public OfficialNoticeId? SupersededBy { get; init; }

    /// <summary>The editor, when there was one. A sweep has none.</summary>
    public UserId? ChangedBy { get; init; }
}
