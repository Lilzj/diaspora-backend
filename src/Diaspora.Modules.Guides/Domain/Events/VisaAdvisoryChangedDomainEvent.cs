using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// A visa or immigration advisory was written, published, edited, archived or restored.
/// <para>
/// <b><see cref="AppliesToCountryCode"/> is why this event exists separately from a notice's.</b>
/// Visa rules are a relation between two countries — the destination and the nationality they apply
/// to — and it is the only content in this phase that is. B14 personalizing "rules that changed for
/// people like you" needs both halves, and a consumer holding only the destination would show a
/// Nigerian passport holder a change that applies to EU nationals.
/// </para>
/// </summary>
public sealed record VisaAdvisoryChangedDomainEvent : DomainEvent
{
    public required VisaAdvisoryId VisaAdvisoryId { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2 — the destination the rules govern entry to.</summary>
    public required string CountryCode { get; init; }

    /// <summary>
    /// The nationality the rules apply to, or null when they apply to everybody. Upper-case ISO
    /// 3166-1 alpha-2.
    /// </summary>
    public string? AppliesToCountryCode { get; init; }

    public required string Title { get; init; }

    /// <summary>The visa category — <c>Student</c>, <c>Skilled Worker</c>. Free text by design.</summary>
    public string? VisaCategory { get; init; }

    /// <summary>One of the verbs in <see cref="IntelligenceChangeKinds"/>.</summary>
    public required string Change { get; init; }

    public required VisaAdvisoryStatus Status { get; init; }

    public required IssuingAuthorityId AuthorityId { get; init; }

    public DateTimeOffset? EffectiveFrom { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public UserId? ChangedBy { get; init; }
}
