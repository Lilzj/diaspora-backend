namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// Records what an interview concluded.
/// <para>
/// The two feedback fields are separate on purpose. <see cref="Feedback"/> is the panel's and is
/// never shown to the candidate; <see cref="CandidateFeedback"/> is the version the employer
/// chose to share, and is usually empty. One field would force a choice between an honest
/// internal note and a kind external one.
/// </para>
/// </summary>
public sealed record RecordInterviewOutcomeRequest
{
    /// <summary><c>Passed</c>, <c>Failed</c>, <c>NoShow</c>, <c>Inconclusive</c>.</summary>
    public required string Outcome { get; init; }

    /// <summary>The panel's notes. Internal.</summary>
    public string? Feedback { get; init; }

    /// <summary>What to share with the candidate, if anything.</summary>
    public string? CandidateFeedback { get; init; }
}
