using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Integration;

namespace Diaspora.Modules.Providers.Extensibility.Search;

/// <summary>
/// How a module adds its own signals to a provider's search projection.
/// <para>
/// <b>The seam that lets B15 and B17 index things B7 has never heard of.</b> B7 builds the
/// projection from what it owns — name, category, tags, languages, services, geography,
/// verification, rating. A recommendation module may want an embedding; a jobs module may want
/// "this provider is hiring"; an events module may want "hosting something this weekend". None
/// of those belong in B7's aggregate, and all of them belong in the document B15 indexes.
/// </para>
/// <para>
/// Contributors write into <see cref="ProviderSearchProjection.Signals"/>, a flat string map, and
/// cannot alter the fields B7 owns. That asymmetry is the contract: the core document stays
/// stable and predictable while the periphery grows, which is what makes the projection safe to
/// freeze.
/// </para>
/// <para>
/// <b>B7 implements no search.</b> It generates a document and publishes it. What tokenises,
/// scores, facets or embeds it is B15's, and this interface is the line between them.
/// </para>
/// </summary>
public interface IProviderSearchProjectionContributor
{
    /// <summary>The module contributing. Also namespaces its signal keys, by convention.</summary>
    string Module { get; }

    /// <summary>Lower runs first. Later contributors see earlier ones' signals and may read them.</summary>
    int Order => 100;

    /// <summary>
    /// Adds signals for one provider.
    /// <para>
    /// Keys should be namespaced by module — <c>jobs.isHiring</c>, <c>ai.embeddingVersion</c> —
    /// because the map is flat and two modules choosing <c>score</c> would silently overwrite
    /// each other in whichever order the container happened to resolve them.
    /// </para>
    /// </summary>
    Task ContributeAsync(
        Provider provider,
        IDictionary<string, string> signals,
        CancellationToken cancellationToken = default);
}
