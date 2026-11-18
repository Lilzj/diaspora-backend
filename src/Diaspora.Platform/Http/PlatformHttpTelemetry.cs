using System.Collections.Frozen;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Reflection;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Platform.Http;

/// <summary>
/// Counts every error response the platform emits.
/// <para>
/// <b>Measured where the platform guarantees everything converges.</b> <see cref="PlatformProblem"/>
/// is the one factory for the one error shape, by design and by its own documentation — the
/// exception middleware, the rate limiter's rejection callback, the host's controllers and a
/// module's minimal-API endpoints all route through it. Counting there means the number cannot
/// drift from reality by somebody adding a sixth way to fail; counting in the exception middleware
/// would have missed the rate limiter entirely, and the 429 rate is one of the five alerts this
/// exists for.
/// </para>
/// <para>
/// <b>Why this instrument had to be added at all.</b> Nothing counted error responses. The
/// closest was <c>diaspora.requests{outcome=failure}</c>, which counts exceptions thrown inside
/// the CQRS pipeline — a different population: it includes domain exceptions that are mapped to a
/// 409 and are entirely normal, and it excludes a 429 from the rate limiter, which never reaches a
/// handler. So "how often does this platform answer with Platform.Unexpected" had no answer, and
/// that is the number report 3's BBL-001 and BBL-002 would have surfaced through (BARCH-008).
/// </para>
/// </summary>
public static class PlatformHttpTelemetry
{
    public const string SourceName = "Diaspora.Platform.Http";

    /// <summary>The tag value every error code that is not the platform's collapses to.</summary>
    public const string DomainCode = "domain";

    private static readonly Meter Meter = new(SourceName);

    /// <summary>
    /// Error responses, tagged by <c>code</c> and <c>status</c>.
    /// <para>
    /// <b>Three of the platform's five launch alerts read this one instrument</b> — the rate of
    /// <c>Platform.Unexpected</c>, the rate of 5xx, and the rate of 429 — which is why it is a
    /// single counter with two dimensions rather than three counters.
    /// </para>
    /// </summary>
    public static readonly Counter<long> ProblemResponses = Meter.CreateCounter<long>(
        "diaspora.http.problem_responses",
        unit: "{response}",
        description: "Error responses emitted, by platform error code and HTTP status.");

    /// <summary>
    /// The codes that get a tag value of their own, read off <see cref="PlatformErrorCodes"/>
    /// rather than listed here.
    /// <para>
    /// <b>Reflected, so the set cannot drift from the codes.</b> A new platform error code becomes
    /// alertable the moment it is declared, with nothing else to remember — which stopped being
    /// hypothetical during the change that added this file: <c>Platform.Resource.Duplicate</c>
    /// landed on master the same day and got its own dimension without an edit here, where a
    /// hand-written list would have quietly filed every duplicate under <see cref="DomainCode"/>.
    /// And it is a set rather than a prefix test because a prefix test would be wrong today:
    /// <see cref="PlatformErrorCodes.StepUpRequired"/> is <c>Identity.StepUp.Required</c>, so
    /// <c>code.StartsWith("Platform.")</c> would file the platform's own step-up refusal under
    /// somebody else's domain.
    /// </para>
    /// </summary>
    private static readonly FrozenSet<string> PlatformCodes = typeof(PlatformErrorCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Records one error response.
    /// <para>
    /// Never throws and never blocks: it is called from the path that is already handling a
    /// failure, and an instrument that could fail there would turn a 409 into a 500.
    /// </para>
    /// </summary>
    public static void Record(string? code, int status) =>
        ProblemResponses.Add(
            1,
            new KeyValuePair<string, object?>("code", Classify(code)),
            new KeyValuePair<string, object?>(
                "status", status.ToString(CultureInfo.InvariantCulture)));

    /// <summary>
    /// Bounds the <c>code</c> dimension.
    /// <para>
    /// <b>The cardinality is the design.</b> There are thirteen platform error codes and roughly
    /// five hundred and ninety domain ones (measured 18 Nov 2026), so tagging the raw code would
    /// mint a time series per distinct failure on an instrument that fires on every error
    /// response — a dimension whose value range is chosen by the thing being measured rather than
    /// by the measurer. It would not fail loudly; it would make the collector expensive and the
    /// dashboards unreadable, which is a poor way to answer a finding about not being able to see.
    /// </para>
    /// <para>
    /// The module prefix was the obvious middle ground and does not survive inspection: there are
    /// forty-seven distinct prefixes and they are not module names — <c>route</c> and
    /// <c>routing</c>, <c>provider</c> and <c>providers</c>, <c>bounds</c>, <c>heat</c>. A
    /// dimension that looks like a module and is not is worse than one value, so a domain failure
    /// is one value and the specific code stays in the log line beside the correlation id, where
    /// somebody investigating one of these will already be.
    /// </para>
    /// </summary>
    private static string Classify(string? code) =>
        code is not null && PlatformCodes.Contains(code) ? code : DomainCode;
}

/// <summary>
/// Subscribes the host's metrics pipeline to the error-response counter.
/// <para>
/// Contributed rather than wired into <c>Program.cs</c>, the same way the outbox, transactions and
/// every module does it. Attributed to <c>Platform.Http</c>: the kernel's contributor validator
/// recognises <c>Platform</c>, <c>Application</c> and <c>Infrastructure</c> as the prefixes that
/// mean "this is the platform rather than a module", and anything else is reported as a
/// contribution attributed to a module nobody registered.
/// </para>
/// </summary>
public sealed class PlatformHttpObservabilityContributor : IObservabilityContributor
{
    public string Module => "Platform.Http";

    public IEnumerable<string> Meters => [PlatformHttpTelemetry.SourceName];
}
