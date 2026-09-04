using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>
/// An advertising campaign moved between states — MON-GAP-001.
/// <para>
/// <b>Not <c>CampaignStatusChangedDomainEvent</c>, which this module already has.</b> That one
/// announces a *fundraising* campaign's lifecycle. Two entities share the noun; the events may not
/// share the type name, or a consumer subscribing to one would silently receive the other.
/// </para>
/// <para>
/// <b>Raised on every transition, with both states, rather than on the two that matter.</b> The
/// register asks for it "so B13 can notify on approval and rejection" — but which transitions are
/// worth a notification is a rule, and rules live in B13's contributors where they can be changed
/// without a release here. Carrying from and to lets that stay true.
/// </para>
/// </summary>
public sealed record AdCampaignStatusChangedDomainEvent : DomainEvent
{
    public required AdCampaignId AdCampaignId { get; init; }

    public required Guid OwnerUserId { get; init; }

    public required string CampaignName { get; init; }

    public required string PreviousStatus { get; init; }

    public required string CurrentStatus { get; init; }

    /// <summary>
    /// The reviewer's words on an approval or a rejection, and the ending's reason otherwise.
    /// <para>
    /// One field rather than two, because a consumer rendering "your campaign was refused: …" and
    /// one rendering "your campaign stopped: …" want the same slot filled by whichever applies.
    /// </para>
    /// </summary>
    public string? Reason { get; init; }
}
