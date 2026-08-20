namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>One poll option, with its share of the vote when the reader may see it.</summary>
public sealed record PollOptionResponse
{
    public required Guid OptionId { get; init; }

    public required string Label { get; init; }

    public required int Position { get; init; }

    public Guid? ImageMediaId { get; init; }

    /// <summary>Null when results are hidden from this reader. Not zero — null.</summary>
    public int? VoteCount { get; init; }

    /// <summary>Zero to one hundred. Null when results are hidden.</summary>
    public double? SharePercent { get; init; }

    public bool VotedByViewer { get; init; }
}
