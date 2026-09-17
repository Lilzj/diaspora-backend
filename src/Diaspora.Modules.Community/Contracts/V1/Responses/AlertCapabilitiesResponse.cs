namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>
/// What the caller may publish on the alert map, decided by the server.
/// <para>
/// Shaped like the other per-caller capability reads on this platform: every "can I" is a field, and
/// every refusal carries its reason as text for the client to print as it is. A client that works out
/// for itself whether somebody may raise an Emergency is describing a decision it did not make, and
/// one that shows a publish button the server will refuse is worse than one that shows nothing.
/// </para>
/// <para>
/// A caller who is not signed in never reaches this: the route answers 401, which is how a client
/// tells "sign in" from "not permitted" (a 200 with <c>canPublish: false</c>).
/// </para>
/// </summary>
public sealed record AlertCapabilitiesResponse
{
    public required bool CanPublish { get; init; }

    /// <summary>Why not, in words to show the member. Null when <see cref="CanPublish"/> is true.</summary>
    public string? CannotPublishReason { get; init; }

    /// <summary>
    /// Whether the caller may publish, or raise an alert to, Emergency — the urgency that appears on
    /// everybody's live map in the area straight away.
    /// </summary>
    public required bool CanPublishEmergency { get; init; }

    /// <summary>Why not. Null when <see cref="CanPublishEmergency"/> is true.</summary>
    public string? CannotPublishEmergencyReason { get; init; }

    /// <summary>The urgencies this caller may choose from, in wire names, lowest first.</summary>
    public required IReadOnlyList<string> UrgencyLevels { get; init; }

    /// <summary>The smallest affected radius an alert may declare, in metres.</summary>
    public required int MinAffectedRadiusMetres { get; init; }

    /// <summary>The largest, in metres.</summary>
    public required int MaxAffectedRadiusMetres { get; init; }
}
