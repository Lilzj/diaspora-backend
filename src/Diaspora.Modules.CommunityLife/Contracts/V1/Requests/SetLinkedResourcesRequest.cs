namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Replace an event's linked resources — job postings, vendor booths, related sessions.</summary>
public sealed record SetLinkedResourcesRequest
{
    public IReadOnlyList<LinkedResourceRequest> Resources { get; init; } = [];
}
