namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// Closes a posting to applications.
/// <para>
/// <see cref="Reason"/> is shown to everyone with a live application. It is optional, but it is
/// the difference between a candidate learning the role was filled and a candidate refreshing a
/// page for a fortnight.
/// </para>
/// </summary>
public sealed record CloseJobRequest
{
    /// <summary><c>filled</c> or <c>withdrawn</c>. Defaults to withdrawn.</summary>
    public string? Outcome { get; init; }

    public string? Reason { get; init; }
}
