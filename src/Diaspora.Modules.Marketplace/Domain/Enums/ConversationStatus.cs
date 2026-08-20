namespace Diaspora.Modules.Marketplace.Domain.Enums;

/// <summary>
/// Where a buyer/seller conversation reference stands.
/// <para>
/// <b>Three values, and none of them says anything about messages.</b> Marketplace does not own
/// chat: it owns the fact that a buyer asked to talk to a seller about a listing, and the handle
/// a Communication module will hang the actual conversation off. <see cref="Requested"/> is what
/// exists on a deployment with no messaging at all — which is every deployment today, and the
/// reason this is a reference rather than a thread.
/// </para>
/// </summary>
public enum ConversationStatus
{
    /// <summary>A buyer asked to talk. No messaging provider has claimed it.</summary>
    Requested = 0,

    /// <summary>A messaging provider claimed it and gave back an external identifier.</summary>
    Open = 1,

    /// <summary>Ended — the listing sold, or a party closed it.</summary>
    Closed = 2
}
