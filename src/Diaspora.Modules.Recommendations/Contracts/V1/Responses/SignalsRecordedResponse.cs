namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// What the signal store did with a client's batch.
/// <para>
/// <b><see cref="Dropped"/> is not an error count and the client must not treat it as one.</b> A
/// member who turned signal sharing off produces drafts that go nowhere; a client that retried on a
/// non-zero drop would retry forever against a preference that will not change. The field exists so
/// a client can stop sending rather than so it can complain.
/// </para>
/// </summary>
public sealed record SignalsRecordedResponse
{
    public required int Accepted { get; init; }

    /// <summary>Dropped by a privacy preference. Not an error — see the class summary.</summary>
    public required int Dropped { get; init; }

    /// <summary>False when the member's preferences mean nothing behavioural will ever be recorded.</summary>
    public required bool IsRecordingEnabled { get; init; }
}
