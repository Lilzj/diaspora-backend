namespace Diaspora.Application.Identity.Models;

/// <summary>
/// Which feature gates are open for a member, and why the closed ones are closed.
/// </summary>
public sealed record FeatureAccessSnapshot
{
    public required IReadOnlyList<string> Granted { get; init; }

    public required IReadOnlyList<FeatureBlockReason> Blocked { get; init; }

    public bool IsGranted(string featureName) =>
        Granted.Contains(featureName, StringComparer.OrdinalIgnoreCase);

    public static readonly FeatureAccessSnapshot None = new()
    {
        Granted = [],
        Blocked = []
    };
}
