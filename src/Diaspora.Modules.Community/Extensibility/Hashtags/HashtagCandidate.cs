namespace Diaspora.Modules.Community.Extensibility.Hashtags;

/// <summary>A tag found in a body, before anything has decided whether to keep it.</summary>
public sealed record HashtagCandidate
{
    /// <summary>As typed, without the leading hash.</summary>
    public required string Raw { get; init; }

    /// <summary>The canonical form. What counts aggregate on.</summary>
    public required string Slug { get; init; }

    public int StartOffset { get; init; }

    public int Length { get; init; }
}
