namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// Shares one thing a member knows about a place.
/// <para>
/// Deliberately short: an observation, not an essay. A member with a paragraph to write has
/// Community, where it can be discussed. Nothing renders until a moderator has read it.
/// </para>
/// </summary>
public sealed record SubmitNeighborhoodInsightRequest
{
    /// <summary>B4's area the insight is about.</summary>
    public required Guid AreaId { get; init; }

    /// <summary>
    /// Safety, Transport, CostOfLiving, Community, Noise, Utilities, Amenities, Schools, Nightlife,
    /// Accessibility, Healthcare or Other.
    /// </summary>
    public required string Category { get; init; }

    /// <summary>What the member wants to say. Up to 400 characters.</summary>
    public required string Body { get; init; }

    /// <summary>An optional short chip, such as "quiet after ten".</summary>
    public string? Label { get; init; }
}
