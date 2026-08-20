namespace Diaspora.Modules.Location.Contracts.V1.Requests;

/// <summary>Turns travel mode on or off, optionally setting the destination in the same call.</summary>
public sealed record SetTravelModeRequest
{
    public required bool Enabled { get; init; }

    /// <summary>
    /// Where the member is travelling. Omit to keep the previous destination — a member who
    /// toggles travel mode off and on again while still abroad expects to be where they were.
    /// </summary>
    public SetCurrentLocationRequest? Destination { get; init; }
}
