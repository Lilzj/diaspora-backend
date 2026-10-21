namespace Diaspora.Modules.Location.Contracts.V1.Requests;

/// <summary>Switches "when the app is open" location on or off — 21 Oct 2026.</summary>
public sealed record SetPresenceRequest
{
    public required bool Enabled { get; init; }
}

/// <summary>
/// Where the device is, sent by the app while it is in the foreground and only once the member has
/// switched presence on — 21 Oct 2026.
/// <para>
/// The point is resolved to a catalogue area and discarded. It is never stored and never published.
/// </para>
/// </summary>
public sealed record PresencePingRequest
{
    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    /// <summary>The device's own accuracy radius. A fix coarser than a kilometre is not evaluated.</summary>
    public double? AccuracyMetres { get; init; }
}
