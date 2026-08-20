using Diaspora.Modules.Conversations.Application.Conversations;
using Diaspora.Modules.Conversations.Contracts.V1.Requests;
using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.ValueObjects;
using Diaspora.Modules.Marketplace.Extensibility.Conversations;
using Diaspora.Platform.Services.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Conversations.Integration.Marketplace;

/// <summary>
/// Fulfils the seam B10 published and left empty.
/// <para>
/// <b>Marketplace shipped <c>IMarketplaceConversationProvider</c> with no implementation and a note
/// in its own architecture guide reading "the chat between buyer and seller — future
/// Communication".</b> <c>ConversationBroker</c> takes it as an optional dependency and does nothing
/// when it is absent, which is why a Marketplace enquiry has always produced a
/// <c>ListingConversation</c> reference with no thread behind it. This class is what that note meant.
/// </para>
/// <para>
/// <b>Marketplace keeps its reference row; this module keeps the conversation.</b> That is not a
/// duplication — the two hold different things. <c>ListingConversation</c> is Marketplace's record
/// that a buyer enquired, indexed by listing and seller, and it drives Marketplace's own seller
/// statistics. It stores an external handle and no messages, which is precisely what "no other
/// module stores conversation messages" permits. The handle it stores is this module's
/// <c>ConversationId</c>.
/// </para>
/// <para>
/// <b>The adapter lives here, not in Marketplace, and it has to.</b> Marketplace is order 900 and
/// cannot reference a module at 1500 without a cycle. Registering an implementation of an earlier
/// module's port from a later one is the same substitution B13's realtime seams use.
/// </para>
/// </summary>
public sealed class MarketplaceConversationAdapter : IMarketplaceConversationProvider
{
    private readonly ISender _sender;
    private readonly IConversationRepository _conversations;
    private readonly IClock _clock;
    private readonly ILogger<MarketplaceConversationAdapter> _logger;

    public MarketplaceConversationAdapter(
        ISender sender,
        IConversationRepository conversations,
        IClock clock,
        ILogger<MarketplaceConversationAdapter> logger)
    {
        _sender = sender;
        _conversations = conversations;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// The name Marketplace stores alongside the handle, so an operator reading a row knows what
    /// produced it.
    /// </summary>
    public string Provider => ConversationsModuleMetadata.Name;

    /// <summary>
    /// Opens the conversation and returns its identifier as the handle.
    /// <para>
    /// Goes through <see cref="CreateConversationCommand"/> rather than touching the repository, so
    /// the block list, the conversation policies, the context-type catalogue and the find-or-create
    /// rule all apply. An adapter that constructed the aggregate itself would be a second way to
    /// open a conversation, exempt from every check the first one makes.
    /// </para>
    /// <para>
    /// Returns null on any failure. <c>ConversationBroker</c> treats that as "no thread yet" and
    /// leaves its reference standing to be attached later — which is the right outcome: a blocked
    /// buyer should not lose their enquiry record, they should lose the chat button.
    /// </para>
    /// </summary>
    public async Task<string?> OpenAsync(
        MarketplaceConversationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(
            new CreateConversationCommand
            {
                RecipientId = request.SellerUserId,
                Context = new ConversationContextRequest
                {
                    Type = ConversationTypeKeys.MarketplaceListing,
                    EntityId = request.ListingId.ToString("D"),
                    EntityTitle = request.ListingTitle,

                    // The seller's name, as Marketplace already knows it. This module resolves no
                    // names of its own — but a caller that has one may put it in the snapshot, and
                    // that is what a snapshot is for.
                    EntitySubtitle = string.IsNullOrWhiteSpace(request.SellerName)
                        ? null
                        : $"Seller: {request.SellerName}",
                    DeepLink = $"/marketplace/listing/{request.ListingId:D}"
                }
            },
            cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogInformation(
                "Opening a conversation for listing {ListingId} was refused: {Code}.",
                request.ListingId,
                result.Error.Code);

            return null;
        }

        return result.Value.ConversationId.ToString("D");
    }

    /// <summary>
    /// Closes the conversation when Marketplace closes its enquiry.
    /// <para>
    /// The listing sold or was withdrawn. The thread stays readable — what was agreed before it sold
    /// is exactly what somebody needs afterwards — and refuses new messages.
    /// </para>
    /// </summary>
    public async Task CloseAsync(
        string externalConversationId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(externalConversationId, out var parsed) || parsed == Guid.Empty)
        {
            return;
        }

        var conversation = await _conversations.GetAsync(
            ConversationId.From(parsed), cancellationToken);

        if (conversation is null || conversation.IsClosed)
        {
            return;
        }

        // A machine token, not a sentence: consumers branch on it and a sentence would be localised.
        conversation.Close("entity_closed", _clock.UtcNow);

        _conversations.Update(conversation);

        // No SaveChanges here. This runs inside Marketplace's own command, whose unit of work commits
        // the listing change and this closure together — which is what makes "the listing sold and
        // the conversation closed" one fact rather than two that can disagree.
    }
}
