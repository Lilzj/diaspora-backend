namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Asks for the guide to be verified. Submission is not approval — the claim goes to whatever
/// verification source the deployment runs (Trust, by default), and the outcome arrives later
/// through integration events.
/// </summary>
public sealed record SubmitGuideVerificationRequest
{
    /// <summary>Supporting evidence, as Media references with declared document types.</summary>
    public IReadOnlyList<GuideVerificationEvidenceInput> Evidence { get; init; } = [];

    /// <summary>Free text for the reviewer. Displayed, never interpreted.</summary>
    public string? Notes { get; init; }
}
