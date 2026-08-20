namespace Diaspora.Modules.Marketplace.Extensibility.Conversations;

/// <summary>
/// The seam a future Communication module lands on.
/// <para>
/// <b>Marketplace does not own chat, and this interface is how it stays that way while still
/// letting a buyer press "message the seller".</b> The module records a conversation reference and
/// asks whatever provider is registered to give it a handle. On a deployment with no messaging at
/// all, none is registered, the reference stays in its requested state, and the client renders a
/// contact preference instead. Nothing breaks and nothing is missing.
/// </para>
/// <para>
/// <b>Last-wins registration, not a collection.</b> There is one messaging system, not a chain of
/// them: a deployment that ships two would have buyers and sellers in different conversations
/// about the same item.
/// </para>
/// <para>
/// The Communication module implements this and B10 does not change. That is the entire
/// integration surface, and it is the reason the brief could say "expose conversation references"
/// rather than "build messaging".
/// </para>
/// </summary>
public interface IMarketplaceConversationProvider
{
    /// <summary>Which provider this is. Recorded on the reference so two never fight over one.</summary>
    string Provider { get; }

    /// <summary>
    /// Opens or finds the conversation for a listing and a buyer, and returns its handle.
    /// <para>
    /// Idempotent on (listing, buyer): a retry after a timeout must return the same handle rather
    /// than start a second conversation the buyer cannot see. Returning null means the provider
    /// declined — the reference stays requested and the client falls back to the contact
    /// preference.
    /// </para>
    /// </summary>
    Task<string?> OpenAsync(
        MarketplaceConversationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes a conversation the module has finished with — the listing sold, or a party ended it.
    /// Best-effort: a failure leaves an open conversation, which is untidy rather than harmful.
    /// </summary>
    Task CloseAsync(string externalConversationId, CancellationToken cancellationToken = default);
}
