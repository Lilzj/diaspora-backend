using Diaspora.Modules.Housing.Domain.ValueObjects;

namespace Diaspora.Modules.Housing.Extensibility.Neighborhoods;

/// <summary>
/// Implemented to supply liveability bands for a neighbourhood from somewhere other than an
/// editor.
/// <para>
/// <b>This is the seam a future neighbourhood-intelligence capability lands on, and shipping it
/// empty is deliberate.</b> The B11 brief puts AI neighbourhood recommendations out of scope and
/// asks that the foundation support them later without restructuring. What "later" needs is a way
/// for something else to answer "how walkable is this place" — a transit dataset, a crime-figures
/// import, a model — without that thing becoming part of Housing or Housing learning what it is.
/// This interface is that way.
/// </para>
/// <para>
/// <b>A provider supplies a suggestion, not a fact.</b> The profile's stored index is the
/// platform's published position on a place, and it is written by a person with the approval
/// permission. A provider's answer is offered to that person; it does not overwrite them. That
/// ordering matters because these bands are published about the places members live, and an
/// unreviewed model output is not something to publish under the platform's name.
/// </para>
/// <para>
/// No implementation ships. On a deployment with none, the profile is written entirely by editors
/// and nothing degrades.
/// </para>
/// </summary>
public interface INeighborhoodMetricProvider
{
    /// <summary>What this provider calls itself, for the reviewer deciding whether to accept it.</summary>
    string Name { get; }

    /// <summary>
    /// Suggested bands for a B4 area, or null when this provider has nothing to say about it.
    /// <para>
    /// Null rather than an unassessed index, so "I do not cover this place" and "I cover it and
    /// everything is unknown" stay distinguishable.
    /// </para>
    /// </summary>
    Task<NeighborhoodLivingIndex?> SuggestAsync(
        Guid areaId, CancellationToken cancellationToken = default);
}
