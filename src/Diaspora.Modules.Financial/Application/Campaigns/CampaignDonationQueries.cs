using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Campaigns;

/// <summary>
/// The caller's own donations to one fundraising campaign — <c>GET /communities/campaigns/{id}/donations/me</c>.
/// <para>
/// <b>A donation belongs to a campaign through the campaign's cause.</b> Publishing a campaign creates
/// a <c>DonationCause</c>, and <c>POST /financial/donations</c> against that cause's code is how money
/// reaches it; so "my donations to this campaign" is "my donations to its cause", read from B15's own
/// rows rather than from a campaign-side copy that could drift from them.
/// </para>
/// <para>
/// <b>Owner only, and the route takes no user.</b> The donor is the caller, so reading somebody
/// else's giving is not expressible. Anonymous donations are absent for the reason
/// <see cref="IDonationRepository.ListByDonorForCauseAsync"/> gives.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.GivingRead)]
public sealed record GetMyCampaignDonationsQuery : IQuery<IReadOnlyList<CampaignDonationResponse>>
{
    public required Guid CampaignId { get; init; }

    public int? Take { get; init; }
}

public sealed class GetMyCampaignDonationsQueryHandler
    : IQueryHandler<GetMyCampaignDonationsQuery, IReadOnlyList<CampaignDonationResponse>>
{
    /// <summary>A member's donations to one campaign. Far more than anybody gives to one cause.</summary>
    private const int DefaultTake = 50;

    private readonly ICampaignRepository _campaigns;
    private readonly IDonationRepository _donations;
    private readonly IPaymentIntentRepository _intents;
    private readonly CampaignAccessService _access;
    private readonly CurrencyCatalog _currencies;

    public GetMyCampaignDonationsQueryHandler(
        ICampaignRepository campaigns,
        IDonationRepository donations,
        IPaymentIntentRepository intents,
        CampaignAccessService access,
        CurrencyCatalog currencies)
    {
        _campaigns = campaigns;
        _donations = donations;
        _intents = intents;
        _access = access;
        _currencies = currencies;
    }

    public async Task<Result<IReadOnlyList<CampaignDonationResponse>>> Handle(
        GetMyCampaignDonationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CampaignDonationResponse>>(caller.Error);
        }

        var campaign = await _campaigns.FindAsync(CampaignId.From(query.CampaignId), cancellationToken);

        if (campaign is null)
        {
            return NotFound();
        }

        var donations = campaign.CauseId is { } causeId
            ? await _donations.ListByDonorForCauseAsync(
                caller.Value,
                causeId,
                Math.Clamp(query.Take ?? DefaultTake, 1, FinancialLimits.MaxLedgerPageSize),
                cancellationToken)
            : [];

        // A campaign the caller cannot see is reported as absent — the same answer the campaign
        // route gives — unless they gave to it. A donor keeps sight of their own giving after a
        // campaign closes, is cancelled or is withdrawn from public view.
        if (donations.Count == 0
            && !campaign.IsPubliclyVisible
            && !await _access.CanManageAsync(campaign, caller.Value, cancellationToken))
        {
            return NotFound();
        }

        var symbols = await _currencies.AllAsync(cancellationToken);
        var responses = new List<CampaignDonationResponse>(donations.Count);

        foreach (var donation in donations)
        {
            var symbol = symbols.TryGetValue(donation.Currency, out var facts) ? facts.Symbol : null;

            var payment = donation.PaymentIntentId is { } intentId
                ? await _intents.FindAsync(intentId, cancellationToken)
                : null;

            var mapped = FinancialMapper.ToResponse(donation, payment, symbol);

            responses.Add(new CampaignDonationResponse
            {
                Donation = mapped,
                CampaignId = campaign.Id.Value,
                CampaignTitle = campaign.Title,
                Status = donation.Status.ToString(),
                Payment = mapped.Payment
            });
        }

        return Result.Success<IReadOnlyList<CampaignDonationResponse>>(responses);
    }

    private static Result<IReadOnlyList<CampaignDonationResponse>> NotFound() =>
        Result.Failure<IReadOnlyList<CampaignDonationResponse>>(Error.NotFound(
            FinancialErrorCodes.CampaignNotFound, "No campaign with that identifier."));
}
