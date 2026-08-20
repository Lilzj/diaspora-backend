namespace Diaspora.Modules.Media.Extensibility.Processing;

/// <summary>
/// Inspects bytes for malware.
/// <para>
/// <b>Media ships no scanner, and that is deliberate.</b> A real scanner is ClamAV in a
/// sidecar, a cloud scanning API, or an endpoint-protection agent — an infrastructure choice
/// with a cost and an operational footprint, not a library a module gets to pick on a
/// deployment's behalf. Registering a fake one that always says "clean" would be worse than
/// having none: it would make an unscanned platform look like a scanned one.
/// </para>
/// <para>
/// With no scanner registered, every asset is marked
/// <see cref="Domain.Enums.VirusScanStatus.Skipped"/> — a distinct status from <c>Clean</c>,
/// reported by the module's readiness check, so "this deployment does not scan uploads" is a
/// visible operational fact rather than an assumption. See the security guide.
/// </para>
/// <para>
/// A scanner that errors must return <see cref="MediaProcessingOutcome.Failed"/> and not a
/// clean verdict. <see cref="Domain.ValueObjects.VirusScanResult.IsSafeToServe"/> treats a
/// failed scan as unsafe, which is what stops a scanner outage becoming a distribution window.
/// </para>
/// </summary>
public interface IVirusScanner : IMediaProcessor
{
    /// <summary>How the scanner identifies itself. Recorded on every verdict it issues.</summary>
    string ScannerName { get; }

    Task<MediaProcessingOutcome> ScanAsync(
        MediaProcessingContext context, CancellationToken cancellationToken = default);
}
