namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// A moderator's decision on a posting.
/// <para>
/// The reason is required on a hide, and appended to the posting's unerasable editorial trail.
/// Fraudulent job adverts are the most damaging content this module can carry, and a takedown
/// nobody can account for six months later is one nobody can review.
/// </para>
/// </summary>
public sealed record ModerateJobRequest
{
    /// <summary><c>hide</c>, <c>unhide</c> or <c>flag</c>.</summary>
    public required string Action { get; init; }

    /// <summary>Required when hiding.</summary>
    public string? Reason { get; init; }
}
