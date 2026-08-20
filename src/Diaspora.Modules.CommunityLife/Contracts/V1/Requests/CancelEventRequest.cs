namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>
/// Call an event off.
/// <para>
/// Terminal. Every response is released and everybody who was coming is told, so an organizer who
/// wants to try again runs a new event rather than un-cancelling this one — which would silently
/// re-invite everybody who had already withdrawn.
/// </para>
/// </summary>
public sealed record CancelEventRequest
{
    /// <summary>Shown to everybody who was coming. Worth writing.</summary>
    public string? Reason { get; init; }
}
