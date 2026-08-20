namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>A recorded share.</summary>
public sealed record ShareResponse
{
    public required Guid ShareId { get; init; }

    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required string Kind { get; init; }

    public string? Destination { get; init; }

    /// <summary>The post a quote share produced.</summary>
    public Guid? QuotePostId { get; init; }

    public required int ShareCount { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
