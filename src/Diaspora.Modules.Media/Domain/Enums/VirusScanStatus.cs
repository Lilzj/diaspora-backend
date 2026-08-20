namespace Diaspora.Modules.Media.Domain.Enums;

/// <summary>
/// The outcome of an anti-malware pass.
/// <para>
/// <see cref="NotScanned"/> and <see cref="Skipped"/> are different states and the difference
/// is the point. <c>NotScanned</c> means the queue has not reached the file yet;
/// <c>Skipped</c> means no scanner is registered in this deployment. The first resolves
/// itself, the second is an operational gap somebody must decide to accept — and a readiness
/// probe can tell them apart.
/// </para>
/// </summary>
public enum VirusScanStatus
{
    NotScanned = 0,
    Scanning = 1,
    Clean = 2,
    Infected = 3,

    /// <summary>The scanner errored. Treated as "not clean" for access decisions.</summary>
    Failed = 4,

    /// <summary>No scanner is registered. The deployment has accepted the risk.</summary>
    Skipped = 5
}
