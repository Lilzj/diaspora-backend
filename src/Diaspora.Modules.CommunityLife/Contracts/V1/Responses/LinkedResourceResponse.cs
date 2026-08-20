namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>
/// Something outside this module that an event points at.
/// <para>
/// The client resolves <see cref="ResourceId"/> against the vertical that owns it. This module does
/// not, and deliberately: the alternative is an edge to Jobs or Marketplace and a validation that
/// goes stale the moment the posting closes.
/// </para>
/// </summary>
public sealed record LinkedResourceResponse
{
    public required string ResourceType { get; init; }

    public Guid? ResourceId { get; init; }

    public string? Label { get; init; }

    public string? Url { get; init; }

    public required int Order { get; init; }
}
