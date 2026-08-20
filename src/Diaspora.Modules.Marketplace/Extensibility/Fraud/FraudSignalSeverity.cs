namespace Diaspora.Modules.Marketplace.Extensibility.Fraud;

/// <summary>
/// How seriously a fraud signal should be taken.
/// <para>
/// Three levels, because that is how many distinct actions the module has: record it, queue it,
/// or hold the listing. A finer scale would be a number nobody could map onto a decision.
/// </para>
/// </summary>
public enum FraudSignalSeverity
{
    /// <summary>Worth recording, worth nothing else.</summary>
    Information = 0,

    /// <summary>Flags the listing into the moderation queue. It stays visible.</summary>
    Warning = 1,

    /// <summary>Holds the listing for review before it reaches a buyer.</summary>
    Severe = 2
}
