namespace Diaspora.Modules.Media.Api;

/// <summary>
/// The body of a quarantine release.
/// <para>
/// A required reason, and not an optional one. Overriding a security control without recording
/// why is how a platform ends up unable to explain, months later, why a flagged file is live.
/// </para>
/// </summary>
public sealed record ReleaseQuarantineBody
{
    public required string Reason { get; init; }
}
