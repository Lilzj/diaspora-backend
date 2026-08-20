namespace Diaspora.Modules.Community.Contracts.V1.Requests;

/// <summary>One option on a poll being created.</summary>
public sealed record PollOptionRequest
{
    public required string Label { get; init; }

    /// <summary>A Media reference. Never a URL.</summary>
    public Guid? ImageMediaId { get; init; }
}
