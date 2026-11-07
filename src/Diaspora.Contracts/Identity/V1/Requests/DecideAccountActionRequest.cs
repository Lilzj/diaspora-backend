namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// A second member of staff confirming or declining a proposed account action, or its proposer
/// withdrawing it.
/// </summary>
public sealed record DecideAccountActionRequest
{
    /// <summary>
    /// What the decider has to say. Optional to confirm or withdraw; required to decline, so the
    /// proposer learns why.
    /// </summary>
    public string? Note { get; init; }
}
