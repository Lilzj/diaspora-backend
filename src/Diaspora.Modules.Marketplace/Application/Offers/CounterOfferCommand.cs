using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Offers;

/// <summary>
/// Answers a negotiation with a different number.
/// <para>
/// Either side may counter. Whose turn it is, is the aggregate's answer rather than the caller's
/// claim, and the permission is the low-floor one — leaving a negotiation must never be blocked.
/// </para>
/// </summary>
[Authorize(Permission = MarketplacePermissions.OfferRespond)]
[Auditable]
public sealed record CounterOfferCommand : ICommand<OfferResponse>
{
    public required Guid OfferId { get; init; }

    public required CounterOfferRequest Request { get; init; }
}
