using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// An issuing authority was recorded, edited, verified, archived or restored.
/// <para>
/// <b>The verification verbs on this event are the ones that matter.</b> An authority moving to
/// verified is the moment every notice it has ever issued becomes publishable, and a consumer
/// showing an "official" badge needs to hear about it — the alternative is a badge that only
/// refreshes when a notice happens to be edited.
/// </para>
/// </summary>
public sealed record IssuingAuthorityChangedDomainEvent : DomainEvent
{
    public required IssuingAuthorityId IssuingAuthorityId { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2 — the country this body issues for.</summary>
    public required string CountryCode { get; init; }

    public required string Name { get; init; }

    public required AuthorityType AuthorityType { get; init; }

    /// <summary>One of the verbs in <see cref="IntelligenceChangeKinds"/>.</summary>
    public required string Change { get; init; }

    public required bool IsVerified { get; init; }

    public UserId? ChangedBy { get; init; }
}
