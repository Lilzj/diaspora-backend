namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>A B8 guide worth reading beside a property.</summary>
public sealed record HousingGuideReferenceResponse
{
    public required Guid GuideId { get; init; }

    public string? Title { get; init; }

    public string? Slug { get; init; }
}
