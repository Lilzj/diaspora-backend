namespace Diaspora.IntegrationTests.Recommendations;

/// <summary>
/// What the signal store did with a batch.
/// <para>
/// <c>Dropped</c> is not an error count — a client that retried on it would retry forever against a
/// preference that will not change.
/// </para>
/// </summary>
public sealed record SignalsRecordedShape
{
    public int Accepted { get; init; }

    public int Dropped { get; init; }

    public bool IsRecordingEnabled { get; init; }
}
