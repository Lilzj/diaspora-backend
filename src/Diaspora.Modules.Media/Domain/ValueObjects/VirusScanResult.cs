using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.ValueObjects;

/// <summary>
/// What a malware scanner concluded, and when.
/// <para>
/// The design decision worth naming is that <see cref="IsSafeToServe"/> is false for every
/// status except <see cref="VirusScanStatus.Clean"/> and
/// <see cref="VirusScanStatus.Skipped"/> — including <see cref="VirusScanStatus.Failed"/>. A
/// scanner that errored has told us nothing, and treating "nothing" as "clean" is how a
/// scanner outage becomes a malware distribution window.
/// </para>
/// <para>
/// <see cref="VirusScanStatus.Skipped"/> is the exception, and it is an explicit, visible
/// deployment choice: no scanner is registered at all, the readiness endpoint reports it, and
/// the alternative — refusing every upload on a deployment with no scanner — makes the module
/// unusable in development.
/// </para>
/// </summary>
public sealed class VirusScanResult : ValueObject
{
    public const int MaxScannerLength = 64;

    public const int MaxThreatLength = 256;

    /// <summary>Nothing has looked at the file yet.</summary>
    public static readonly VirusScanResult NotScanned =
        new(VirusScanStatus.NotScanned, null, null, null);

    private VirusScanResult(
        VirusScanStatus status, string? scanner, string? threatName, DateTimeOffset? scannedAt)
    {
        Status = status;
        Scanner = scanner;
        ThreatName = threatName;
        ScannedAt = scannedAt;
    }

    public VirusScanStatus Status { get; }

    /// <summary>Which scanner produced this verdict. Needed to interpret a false positive.</summary>
    public string? Scanner { get; }

    /// <summary>The scanner's name for what it found.</summary>
    public string? ThreatName { get; }

    public DateTimeOffset? ScannedAt { get; }

    /// <summary>
    /// Whether the bytes may be handed to anyone. Deliberately not the negation of "infected".
    /// </summary>
    public bool IsSafeToServe =>
        Status is VirusScanStatus.Clean or VirusScanStatus.Skipped;

    /// <summary>Whether the asset must be quarantined rather than merely held back.</summary>
    public bool RequiresQuarantine => Status is VirusScanStatus.Infected;

    public static VirusScanResult Scanning(string scanner, DateTimeOffset startedAt) =>
        new(VirusScanStatus.Scanning, Trim(scanner, MaxScannerLength), null, startedAt);

    public static VirusScanResult Clean(string scanner, DateTimeOffset scannedAt) =>
        new(VirusScanStatus.Clean, Trim(scanner, MaxScannerLength), null, scannedAt);

    public static VirusScanResult Infected(string scanner, string threatName, DateTimeOffset scannedAt) =>
        new(
            VirusScanStatus.Infected,
            Trim(scanner, MaxScannerLength),
            Trim(threatName, MaxThreatLength),
            scannedAt);

    public static VirusScanResult Failed(string scanner, string reason, DateTimeOffset scannedAt) =>
        new(
            VirusScanStatus.Failed,
            Trim(scanner, MaxScannerLength),
            Trim(reason, MaxThreatLength),
            scannedAt);

    /// <summary>No scanner is registered in this deployment.</summary>
    public static VirusScanResult Skipped(DateTimeOffset at) =>
        new(VirusScanStatus.Skipped, null, null, at);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Status;
        yield return Scanner;
        yield return ThreatName;
        yield return ScannedAt;
    }

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
