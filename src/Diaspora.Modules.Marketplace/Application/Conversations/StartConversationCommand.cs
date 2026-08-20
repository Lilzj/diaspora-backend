using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Conversations;

/// <summary>
/// Asks to talk to a seller about a listing.
/// <para>
/// Gated on the offer permission rather than a messaging one, deliberately: this module owns no
/// messaging capability and inventing a <c>marketplace:message</c> permission would be it claiming
/// one. What the caller is doing is initiating contact about a trade, which is the same band.
/// </para>
/// </summary>
[Authorize(Permission = MarketplacePermissions.OfferRespond)]
public sealed record StartConversationCommand : ICommand<ConversationReferenceResponse>
{
    public required Guid ListingId { get; init; }

    public StartConversationRequest? Request { get; init; }
}
