namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// Agreeing, or withdrawing agreement, with a published insight.
/// <para>
/// Endorsement is what turns one member opinion into something the profile can rank by. It is
/// deliberately not a B6 reaction: a reaction is a response to content, and this is a second
/// resident attesting to a claim about a place.
/// </para>
/// </summary>
public sealed record EndorseInsightRequest
{
    /// <summary>False to withdraw an endorsement. The row is hard-deleted when it is.</summary>
    public bool Endorsed { get; init; } = true;
}
