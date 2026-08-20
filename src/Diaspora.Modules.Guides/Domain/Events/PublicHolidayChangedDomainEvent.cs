using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// A public holiday or official closure was published, edited, archived or restored.
/// <para>
/// <b>The closure flags travel on the event because they are the whole reason anybody consumes
/// it.</b> "Independence Day, 12 March" is a fact about a country; "the passport office is shut on
/// 12 March" is the fact that changes what a member does this week, and a consumer that had to load
/// the entry to find out would be loading it for every holiday in the calendar.
/// </para>
/// </summary>
public sealed record PublicHolidayChangedDomainEvent : DomainEvent
{
    public required PublicHolidayId PublicHolidayId { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    public required string Name { get; init; }

    public required HolidayKind Kind { get; init; }

    /// <summary>The day it falls on. Date-only in meaning; midnight UTC on the wire.</summary>
    public required DateTimeOffset ObservedOn { get; init; }

    /// <summary>The last day of a multi-day observance, or the same day as a single one.</summary>
    public required DateTimeOffset ObservedUntil { get; init; }

    /// <summary>One of the verbs in <see cref="IntelligenceChangeKinds"/>.</summary>
    public required string Change { get; init; }

    public required bool ClosesGovernmentOffices { get; init; }

    public required bool ClosesEmbassies { get; init; }

    public UserId? ChangedBy { get; init; }
}
