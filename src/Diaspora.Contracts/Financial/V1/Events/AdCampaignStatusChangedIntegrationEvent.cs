using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// An advertising campaign moved between states — MON-GAP-001.
/// <para>
/// <b>Distinct from <see cref="CampaignStatusChangedIntegrationEvent"/>, which is a fundraising
/// campaign.</b> Two entities on this platform are called a campaign; a consumer subscribing to the
/// wrong one would receive a lifecycle it cannot interpret and would notify the wrong person about
/// the wrong thing.
/// </para>
/// <para>
/// Carries both states so a consumer decides which transitions are worth telling somebody about.
/// The register wanted approval and rejection; keeping the rule in B13 means the next one does not
/// need a change here.
/// </para>
/// </summary>
public sealed record AdCampaignStatusChangedIntegrationEvent : IntegrationEvent
{
    public required Guid AdCampaignId { get; init; }

    public required Guid OwnerUserId { get; init; }

    public required string CampaignName { get; init; }

    public required string PreviousStatus { get; init; }

    public required string CurrentStatus { get; init; }

    /// <summary>The reviewer's note, or the reason it ended. Rendered verbatim.</summary>
    public string? Reason { get; init; }
}
