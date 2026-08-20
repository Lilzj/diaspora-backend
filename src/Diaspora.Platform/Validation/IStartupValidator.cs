namespace Diaspora.Platform.Validation;

/// <summary>
/// One check the platform runs against itself before it serves a request.
/// <para>
/// <b>This is a contributor seam like every other in B2.6.</b> Implementations are discovered from
/// the platform's own assemblies and resolved from a scope, so a module that wants its own
/// invariant checked at boot adds a class and nothing else. The kernel ships six; nothing about
/// the mechanism knows that.
/// </para>
/// <para>
/// <b>A validator answers, it does not decide.</b> It returns findings and the runner decides what
/// stops the host — which is what keeps the severity policy in one place, makes the whole report
/// available in one log entry rather than as the first exception thrown, and means a validator
/// cannot take the process down by being wrong in an unusual deployment.
/// </para>
/// <para>
/// Implementations must be safe to run before migrations have been applied. The host starts
/// hosted services before anything guarantees a schema exists, and a validator that queried a
/// table would fail every cold start.
/// </para>
/// </summary>
public interface IStartupValidator
{
    /// <summary>A short name for logs and for the report. Conventionally the type name.</summary>
    string Name => GetType().Name;

    /// <summary>
    /// Runs the check. Returning an empty sequence means "nothing to say", which is the normal
    /// result and must be cheap.
    /// </summary>
    IEnumerable<StartupFinding> Validate(StartupValidationContext context);
}
