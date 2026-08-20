namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// What happened to a reported interaction.
/// <para>
/// <see cref="SuppressedUntil"/> is returned on a dismissal so the client can tell the member how
/// long "not this" lasts — which is the difference between a control they trust and one they use
/// once and then wonder about.
/// </para>
/// </summary>
public sealed record FeedbackRecordedResponse
{
    public required Guid FeedbackId { get; init; }

    public required string Kind { get; init; }

    /// <summary>Dismissals only. Null means the member will not be shown it again.</summary>
    public DateTimeOffset? SuppressedUntil { get; init; }

    /// <summary>False when an identical report already existed and this one changed nothing.</summary>
    public required bool WasRecorded { get; init; }
}
