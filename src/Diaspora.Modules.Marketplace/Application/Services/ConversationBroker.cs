using Diaspora.Modules.Marketplace.Domain.Conversations;
using Diaspora.Modules.Marketplace.Extensibility.Conversations;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Marketplace.Application.Services;

/// <summary>
/// The one class that talks to whatever messaging provider a deployment has.
/// <para>
/// <b>On today's deployments there is none, and everything still works.</b> The provider is
/// injected nullable; with none registered a conversation reference stays in its requested state
/// and the client falls back to the seller's contact preference. That is the whole reason the brief
/// could say "expose conversation references" rather than "build messaging".
/// </para>
/// <para>
/// Failures are swallowed. A messaging provider that is briefly unreachable should cost the buyer a
/// chat button, not the ability to enquire at all — and the reference persists either way, so a
/// later attach picks up exactly where this left off.
/// </para>
/// </summary>
public sealed class ConversationBroker
{
    private readonly IMarketplaceConversationProvider? _provider;
    private readonly ILogger<ConversationBroker> _logger;

    public ConversationBroker(
        ILogger<ConversationBroker> logger,
        IMarketplaceConversationProvider? provider = null)
    {
        _provider = provider;
        _logger = logger;
    }

    /// <summary>Whether this deployment can actually carry a conversation.</summary>
    public bool IsAvailable => _provider is not null;

    /// <summary>
    /// Asks the provider to open the conversation and records whatever handle comes back.
    /// <para>
    /// Mutates the aggregate and does not save: the caller owns the unit of work, which is what
    /// keeps the reference and its handle in one transaction.
    /// </para>
    /// </summary>
    public async Task TryAttachAsync(
        ListingConversation conversation,
        string listingTitle,
        string sellerName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        if (_provider is null)
        {
            return;
        }

        try
        {
            var handle = await _provider.OpenAsync(
                new MarketplaceConversationRequest
                {
                    ConversationId = conversation.Id.Value,
                    ListingId = conversation.ListingId.Value,
                    ListingTitle = listingTitle,
                    BuyerUserId = conversation.BuyerUserId.Value,
                    SellerUserId = conversation.SellerUserId.Value,
                    SellerId = conversation.SellerId.Value,
                    SellerName = sellerName
                },
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(handle))
            {
                conversation.Attach(_provider.Provider, handle, conversation.LastActivityAt);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A messaging outage costs a chat button, not an enquiry.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Opening a conversation for listing {ListingId} failed. The reference stands and "
                + "can be attached later.",
                conversation.ListingId.Value);
        }
#pragma warning restore CA1031
    }

    /// <summary>Closes a conversation the module has finished with. Best-effort.</summary>
    public async Task TryCloseAsync(
        ListingConversation conversation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        if (_provider is null || conversation.ExternalConversationId is not { } handle)
        {
            return;
        }

        try
        {
            await _provider.CloseAsync(handle, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // An open conversation is untidy rather than harmful.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception, "Closing conversation {Handle} failed.", handle);
        }
#pragma warning restore CA1031
    }
}
