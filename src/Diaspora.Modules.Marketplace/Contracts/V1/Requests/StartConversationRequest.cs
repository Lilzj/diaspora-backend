namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>
/// Asks to talk to a seller about a listing.
/// <para>
/// <b>The response is a reference, not a thread.</b> Marketplace does not own chat: it records that
/// two members want to talk about one item and hands the reference to whatever messaging provider
/// is registered. On a deployment with none — which is every deployment today — the reference
/// stays requested and the client renders the seller's contact preference instead.
/// </para>
/// </summary>
public sealed record StartConversationRequest
{
    /// <summary>
    /// The buyer's opening question. Held on the reference for a provider that wants it, and
    /// never delivered by this module, which delivers nothing.
    /// </summary>
    public string? Message { get; init; }
}
