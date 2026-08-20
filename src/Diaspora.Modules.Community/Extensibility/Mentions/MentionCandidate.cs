namespace Diaspora.Modules.Community.Extensibility.Mentions;

/// <summary>An at-handle found in a body, before anything has claimed it.</summary>
public sealed record MentionCandidate
{
    /// <summary>As typed, without the leading at-sign.</summary>
    public required string Handle { get; init; }

    public int StartOffset { get; init; }

    public int Length { get; init; }
}
