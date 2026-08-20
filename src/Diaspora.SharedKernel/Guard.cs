namespace Diaspora.SharedKernel;

/// <summary>
/// Guard clause utilities for fail-fast validation.
/// </summary>
public static class Guard
{
    public static GuardClause Against { get; } = new();
}
