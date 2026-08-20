using Diaspora.Modules.Recommendations.Domain.Enums;

namespace Diaspora.Modules.Recommendations.Extensibility.Signals;

/// <summary>
/// How much each kind of observation is worth.
/// <para>
/// <b>Its own seam because these numbers encode a theory of intent, and the theory is
/// arguable.</b> The shipped implementation says an application is worth roughly five times a view
/// and a dismissal is a strong negative. Whether a bookmark is closer to a view or to an application
/// is a genuinely open question that a deployment with data may answer differently from one without,
/// and the difference propagates through every affinity in every profile.
/// </para>
/// <para>
/// <b>Also where a client is not trusted.</b> A signal recorded through the public endpoint carries
/// whatever weight the client claimed; this is what clamps it, so a modified app cannot inflate its
/// own interests by recording a hundred maximum-weight views.
/// </para>
/// </summary>
public interface ISignalWeightPolicy
{
    /// <summary>The default weight for a kind, between 0 and 1.</summary>
    decimal WeightFor(SignalKind kind);

    /// <summary>
    /// The highest weight a source is permitted to claim.
    /// <para>
    /// A module publishing a contract is trusted; a client is not. See the class summary.
    /// </para>
    /// </summary>
    decimal CeilingFor(SignalSource source);

    /// <summary>
    /// How fast a kind's contribution decays.
    /// <para>
    /// Per kind rather than global, because they age differently: a country is close to permanent,
    /// a search term is stale in a week.
    /// </para>
    /// </summary>
    TimeSpan HalfLifeFor(SignalKind kind);
}
