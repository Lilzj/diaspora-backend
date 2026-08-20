namespace Diaspora.Modules.GeographicIntelligence.Domain.Routing;

/// <summary>
/// How somebody is travelling.
/// <para>
/// Four, matching the modes the frontend's routing UX already switches between. There is no
/// <c>Unknown</c> and no default: a caller that did not say how it is travelling is asking a
/// question with four different answers, and picking one for it would be the server guessing at a
/// journey it cannot see.
/// </para>
/// </summary>
public enum TravelMode
{
    Walking = 1,
    Cycling = 2,
    Driving = 3,

    /// <summary>
    /// Public transport.
    /// <para>
    /// Listed with the others and separated everywhere it matters. Transit needs a timetable, a
    /// stop inventory and a service calendar; a provider that answers the other three may answer
    /// none of this one, and per-city coverage varies inside a single provider's territory. A
    /// provider declares transit support explicitly rather than inheriting it.
    /// </para>
    /// </summary>
    Transit = 4
}
