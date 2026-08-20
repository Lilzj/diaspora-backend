namespace Diaspora.Modules.Providers.Domain.Enums;

/// <summary>
/// Where a provider sits in its lifecycle.
/// <para>
/// Distinguished from <see cref="ProviderVisibilityLevel"/> on purpose, because they answer
/// different questions and a single field cannot answer both. Status is "is this provider
/// operating"; visibility is "who is allowed to see it". A provider can be
/// <see cref="Active"/> and visible only to one country community, and it can be
/// <see cref="Paused"/> while still publicly listed with a "temporarily closed" banner — which
/// is exactly what a restaurant on holiday wants and what a single field would force it to
/// choose against.
/// </para>
/// <para>
/// <see cref="Suspended"/> is reachable only by staff, and <see cref="Closed"/> only by an
/// owner. Keeping them apart matters when the provider appeals: a suspension is a decision
/// somebody made about them, a closure is a decision they made themselves, and the screens,
/// the notifications and the audit trail differ.
/// </para>
/// </summary>
public enum ProviderStatus
{
    /// <summary>Being written. Visible only to its owners and managers, never in discovery.</summary>
    Draft = 1,

    /// <summary>Submitted and awaiting a first review. Not yet discoverable.</summary>
    PendingReview = 2,

    /// <summary>Operating and discoverable. The only status that appears in search by default.</summary>
    Active = 3,

    /// <summary>
    /// Temporarily not operating, by the owner's choice. Still discoverable, marked closed.
    /// A seasonal business, a holiday, a refurbishment.
    /// </summary>
    Paused = 4,

    /// <summary>Withdrawn from discovery by staff, pending an appeal or a correction.</summary>
    Suspended = 5,

    /// <summary>Permanently stopped operating, by the owner's choice. Kept for its reviews.</summary>
    Closed = 6,

    /// <summary>Removed from every surface. Retained only for audit and restoration.</summary>
    Archived = 7
}
