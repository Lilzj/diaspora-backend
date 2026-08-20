namespace Diaspora.Modules.Location.Contracts.V1.Requests;

/// <summary>Saves or moves a labelled place.</summary>
public sealed record SaveLocationRequest
{
    /// <summary>
    /// The place to save. Omit to save wherever the member currently is — which is what the
    /// selector's "SAVE ACTIVE" button means, and saves the client re-sending a location the
    /// server already holds.
    /// </summary>
    public SetCurrentLocationRequest? Location { get; init; }
}
