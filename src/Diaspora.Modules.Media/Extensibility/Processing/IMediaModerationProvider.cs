namespace Diaspora.Modules.Media.Extensibility.Processing;

/// <summary>
/// Judges whether an asset's contents are acceptable.
/// <para>
/// <b>The boundary here is the point.</b> A moderation provider returns a signal; it does not
/// decide anything. Trust owns moderation on this platform — the queue, the decisions, the
/// appeals — and a Media module that quarantined assets on its own judgement would be a second
/// moderation system with no queue, no appeal and no audit trail.
/// </para>
/// <para>
/// So the flow is: this provider produces a finding, the finding is recorded on the processing
/// job, and the resulting domain event is what Trust subscribes to. Whether a flagged
/// photograph comes down is Trust's call, made through Trust's workflow, with Trust's appeal
/// path intact.
/// </para>
/// <para>
/// The one exception is malware, which is not a moderation judgement — see
/// <see cref="IVirusScanner"/>. Infected bytes are quarantined by Media immediately, because
/// there is no editorial question to ask about them.
/// </para>
/// </summary>
public interface IMediaModerationProvider : IMediaProcessor
{
    Task<MediaProcessingOutcome> ModerateAsync(
        MediaProcessingContext context, CancellationToken cancellationToken = default);
}
