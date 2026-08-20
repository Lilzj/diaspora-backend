namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>One feed a client can ask for. Read from the capabilities route.</summary>
public sealed record FeedDescriptorResponse
{
    public required string Key { get; init; }

    public required string DefaultRanking { get; init; }

    /// <summary>True when the feed needs a signed-in reader.</summary>
    public required bool RequiresViewer { get; init; }
}
