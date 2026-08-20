namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Report an event or an organization.</summary>
public sealed record ReportSubjectRequest
{
    /// <summary>
    /// <c>Spam</c>, <c>Duplicate</c>, <c>Fraud</c>, <c>Impersonation</c>,
    /// <c>InappropriateContent</c>, <c>HateSpeech</c>, <c>Misinformation</c>,
    /// <c>EventDidNotHappen</c>, <c>Other</c>.
    /// </summary>
    public required string Reason { get; init; }

    public string? Detail { get; init; }
}
