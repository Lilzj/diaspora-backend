namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// One of the caller's own donations, in the context of the campaign it was given to.
/// <para>
/// <b>The donation is B15's record, unchanged.</b> The campaign fields around it are what the
/// campaign screen needs to label it; the receipt a member sees always renders from
/// <see cref="Donation"/>. <see cref="Status"/> repeats the donation's status so the client need
/// not reach into the nested record to colour a row.
/// </para>
/// </summary>
public sealed record CampaignDonationResponse
{
    public required DonationResponse Donation { get; init; }

    public required Guid CampaignId { get; init; }

    public required string CampaignTitle { get; init; }

    /// <summary>The donation's <c>GivingStatus</c>.</summary>
    public required string Status { get; init; }

    /// <summary>The payment behind the donation, when one was opened. Also on <see cref="Donation"/>.</summary>
    public PaymentIntentResponse? Payment { get; init; }
}
