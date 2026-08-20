using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// A mission in the embassy directory was recorded, edited, verified, archived or restored.
/// <para>
/// <b>Two country codes, and both are load-bearing.</b> <see cref="CountryCode"/> is where the
/// mission physically is; <see cref="ServesCountryCode"/> is whose nationals it serves. The Nigerian
/// High Commission in London appears on the United Kingdom's dashboard as a mission present there,
/// and on Nigeria's as somewhere its citizens abroad are looked after — and a consumer holding one
/// code cannot derive the other.
/// </para>
/// </summary>
public sealed record EmbassyChangedDomainEvent : DomainEvent
{
    public required EmbassyId EmbassyId { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2 — where the mission is located.</summary>
    public required string CountryCode { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2 — whose nationals it serves.</summary>
    public required string ServesCountryCode { get; init; }

    public required string Name { get; init; }

    /// <summary>One of the verbs in <see cref="IntelligenceChangeKinds"/>.</summary>
    public required string Change { get; init; }

    public required bool IsVerified { get; init; }

    public UserId? ChangedBy { get; init; }
}
