namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>A place the member has labelled and kept.</summary>
public sealed record SavedLocationResponse
{
    public required Guid Id { get; init; }

    /// <summary>The member's own label — conventionally Home, Work or School.</summary>
    public required string Label { get; init; }

    public required LocationReferenceResponse Location { get; init; }

    public required DateTimeOffset SavedAt { get; init; }
}
