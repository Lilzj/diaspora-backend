using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// Guides relevant to where the caller is, in whatever terms the client has.
/// <para>
/// The point of the screen is that a member who has just landed somewhere gets the procedures
/// that apply to <em>there</em> — residence registration in Berlin is not residence registration
/// in Lisbon. Every geographic field is optional and every combination is accepted; all of it
/// goes to B4 and is normalised once.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record ListNearbyGuidesQuery : IQuery<CursorPagedResult<GuideCardResponse>>
{
    public required ListNearbyGuidesRequest Request { get; init; }

    /// <summary>Opaque. From a previous page's <c>nextCursor</c>.</summary>
    public string? Cursor { get; init; }
}
