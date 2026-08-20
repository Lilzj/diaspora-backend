using Diaspora.Modules.Housing.Domain.Enums;

namespace Diaspora.Modules.Housing.Extensibility.Fraud;

/// <summary>One thing a provider noticed.</summary>
public sealed record HousingFraudSignal
{
    /// <summary>A stable code for the kind of signal. Switched on by moderation tooling.</summary>
    public required string Code { get; init; }

    /// <summary>
    /// What a moderator reads. Written for a person deciding in thirty seconds whether to hold a
    /// listing, not for a log.
    /// </summary>
    public required string Description { get; init; }

    public required HousingFraudSignalSeverity Severity { get; init; }

    /// <summary>Which provider raised it. Filled in by the assessor, not by the provider.</summary>
    public string? Source { get; init; }

    public static HousingFraudSignal Informational(string code, string description) =>
        new()
        {
            Code = code,
            Description = description,
            Severity = HousingFraudSignalSeverity.Informational
        };

    public static HousingFraudSignal Suspicious(string code, string description) =>
        new()
        {
            Code = code,
            Description = description,
            Severity = HousingFraudSignalSeverity.Suspicious
        };

    /// <summary>
    /// A signal that stops the publication where the deployment enforces signals. Reserved for
    /// things a provider is close to certain about — a known scam text, a landlord already
    /// suspended elsewhere — because the cost of a false positive is a genuine landlord who leaves.
    /// </summary>
    public static HousingFraudSignal Blocking(string code, string description) =>
        new()
        {
            Code = code,
            Description = description,
            Severity = HousingFraudSignalSeverity.Blocking
        };
}
