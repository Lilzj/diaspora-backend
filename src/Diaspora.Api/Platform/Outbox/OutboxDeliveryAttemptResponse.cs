namespace Diaspora.Api.Platform.Outbox;

/// <summary>
/// One recorded publish attempt.
/// <para>
/// Empty unless attempt recording is switched on. The endpoint still exists in that case and
/// returns nothing, which is more useful than a 404: it tells an operator the feature is off
/// rather than that the message is unknown.
/// </para>
/// </summary>
public sealed record OutboxDeliveryAttemptResponse
{
    public required int AttemptNumber { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required double DurationMilliseconds { get; init; }

    public required bool Succeeded { get; init; }

    public string? Error { get; init; }

    /// <summary>
    /// Which worker made the attempt. In a multi-instance deployment this is what turns "it
    /// fails intermittently" into "it fails on one pod".
    /// </summary>
    public required string WorkerId { get; init; }
}
