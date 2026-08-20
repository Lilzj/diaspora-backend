namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>
/// One subject area's settings, with everything the screen needs to render the row.
/// <para>
/// <c>enabled</c> is the single boolean the existing settings screen switches on; the per-channel
/// flags below it are the finer model a later client can address. <c>isMandatory</c> is why a row
/// renders disabled — a security notification cannot be muted, and saying so beats a control that
/// silently does nothing.
/// </para>
/// </summary>
public sealed record CategoryPreferenceResponse
{
    public required string Key { get; init; }

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    /// <summary>The coarse switch: false only when the member muted the whole category.</summary>
    public required bool Enabled { get; init; }

    public required bool InApp { get; init; }

    public required bool Push { get; init; }

    public required bool Email { get; init; }

    /// <summary>Collect rather than send. Applies to Normal and Low messages only.</summary>
    public required bool UseDigest { get; init; }

    /// <summary>True when the platform will not let this be switched off.</summary>
    public required bool IsMandatory { get; init; }

    public required int Position { get; init; }
}
