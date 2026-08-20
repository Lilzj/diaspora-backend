namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>
/// Something outside this module that an event points at.
/// <para>
/// The identifier is stored opaquely and is deliberately not validated against the owning module:
/// validating a job posting would mean referencing Jobs, which would be the first cycle in a graph
/// B2.6 exists to keep acyclic, and would buy a check that goes stale the moment the posting closes.
/// The client resolves it at render time against the module that owns it.
/// </para>
/// </summary>
public sealed record LinkedResourceRequest
{
    /// <summary>
    /// <c>JobPosting</c>, <c>MarketplaceListing</c>, <c>Event</c>, <c>ExternalLink</c>.
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>The owning module's identifier. Omitted for an external link.</summary>
    public Guid? ResourceId { get; init; }

    /// <summary>What to call it. Supplied by the organizer, because this module cannot look it up.</summary>
    public string? Label { get; init; }

    /// <summary>Set only for an external link, and only when it is a valid http(s) URL.</summary>
    public string? Url { get; init; }
}
