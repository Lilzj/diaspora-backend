using Diaspora.Modules.Community.Domain.Enums;

namespace Diaspora.Modules.Community.Extensibility.Moderation;

/// <summary>A review provider's answer.</summary>
public sealed record ContentReviewResult
{
    public required ContentReviewStatus Status { get; init; }

    public IReadOnlyCollection<ContentReviewFlag> Flags { get; init; } = [];

    /// <summary>Zero to one. Only meaningful when flagged or rejected.</summary>
    public double Confidence { get; init; }

    /// <summary>
    /// The model or ruleset version. Recorded on the review row, because an appeal against an
    /// automated decision cannot be answered without knowing which model made it.
    /// </summary>
    public string? ProviderVersion { get; init; }

    public static ContentReviewResult Clean { get; } =
        new() { Status = ContentReviewStatus.Clean };

    public static ContentReviewResult Flagged(
        IReadOnlyCollection<ContentReviewFlag> flags, double confidence) =>
        new() { Status = ContentReviewStatus.Flagged, Flags = flags, Confidence = confidence };
}
