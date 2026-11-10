using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Marketplace.Application.Conversations;

/// <summary>
/// The caller's conversation references, whichever side of them they are on.
/// <para>
/// References, not threads. What comes back is a list of items two people wanted to talk about and
/// whatever handles a messaging provider issued — never a message.
/// </para>
/// </summary>
[RequirePermission(MarketplacePermissions.OfferRespond)]
public sealed record ListConversationsQuery
    : IQuery<CursorPagedResult<ConversationReferenceResponse>>
{
    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
