namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>The candidate answers an offer.</summary>
public sealed record RespondToOfferRequest
{
    public required bool Accept { get; init; }

    /// <summary>Optional either way. A declined offer needs no justification.</summary>
    public string? Reason { get; init; }
}
