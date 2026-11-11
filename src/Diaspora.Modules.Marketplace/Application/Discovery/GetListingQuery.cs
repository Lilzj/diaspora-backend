using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Discovery;

/// <summary>
/// One listing in full.
/// <para>
/// <b>Carries no authorisation attribute, and that is the module's whole point.</b> Published
/// listings are public: somebody deciding whether this platform is worth joining should be able to
/// see what is for sale before anybody asks them to prove who they are. Visibility is applied inside
/// the handler — a draft, an unlisted listing and a hidden one resolve as <em>not found</em> to
/// everyone but the owning seller and staff, because a 403 would confirm the listing exists.
/// </para>
/// </summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record GetListingQuery : IQuery<ListingResponse>
{
    public Guid? ListingId { get; init; }

    /// <summary>The deep-link handle. Either this or the identifier.</summary>
    public string? Slug { get; init; }

    /// <summary>
    /// A key identifying the viewer for unique-view de-duplication — a session or address hash for
    /// an anonymous reader. Never persisted; held only for the de-duplication window.
    /// </summary>
    public string? ViewerKey { get; init; }
}
