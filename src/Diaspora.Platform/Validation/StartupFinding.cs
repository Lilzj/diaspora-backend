namespace Diaspora.Platform.Validation;

/// <summary>How much a finding matters.</summary>
public enum StartupFindingSeverity
{
    /// <summary>
    /// Worth saying and not worth stopping for. A vocabulary drift, a contributor attributed to a
    /// module nobody registered, a seeder registered by hand where discovery would do.
    /// </summary>
    Warning,

    /// <summary>
    /// The host must not start. Reserved for states where continuing means serving requests against
    /// a platform that is wrong rather than incomplete: a duplicate permission key, a policy nothing
    /// registered, a staff capability open to members.
    /// </summary>
    Error
}

/// <summary>
/// One thing a validator found.
/// <para>
/// <b>A code, a subject and a sentence, because the audience is somebody reading a crashed boot
/// log at an unsociable hour.</b> The code is greppable and stable, the subject names the exact
/// permission or contributor or policy at fault, and the message says what to do. A finding that
/// says "invalid configuration" has told that person nothing they did not already know.
/// </para>
/// </summary>
/// <param name="Code">Stable identifier for the rule, e.g. <c>SV3002</c>.</param>
/// <param name="Severity">Whether the host may continue.</param>
/// <param name="Subject">The permission, gate, policy, contributor or module at fault.</param>
/// <param name="Message">What is wrong and what would fix it.</param>
public sealed record StartupFinding(
    string Code,
    StartupFindingSeverity Severity,
    string Subject,
    string Message)
{
    public static StartupFinding Error(string code, string subject, string message) =>
        new(code, StartupFindingSeverity.Error, subject, message);

    public static StartupFinding Warning(string code, string subject, string message) =>
        new(code, StartupFindingSeverity.Warning, subject, message);

    public override string ToString() => $"{Code} [{Severity}] {Subject}: {Message}";
}
