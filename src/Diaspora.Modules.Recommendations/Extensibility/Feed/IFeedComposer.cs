using Diaspora.Modules.Recommendations.Extensibility.Personalization;

namespace Diaspora.Modules.Recommendations.Extensibility.Feed;

/// <summary>
/// Builds the home feed out of sections.
/// <para>
/// <b>Composition is not ranking, and this is where the two are kept apart.</b> The engine decides
/// which six jobs; this decides whether the jobs row appears at all, where it sits relative to the
/// guides row, and what happens when it comes back with one item. Merging them would put "should
/// this member see a housing section" inside a scorer, where it would be a weight rather than a
/// decision.
/// </para>
/// <para>
/// <b>Sections are filled concurrently and independently.</b> One section returning nothing is
/// normal — a country with no organizations yet — and must not empty the feed. A section that throws
/// is dropped with a logged error rather than failing the request, because a home screen that fails
/// entirely because the events index is unavailable is worse in every way than one missing a row.
/// </para>
/// </summary>
public interface IFeedComposer
{
    /// <summary>
    /// Assembles the feed.
    /// <para>
    /// Returns sections in display order, with the empty ones already dropped. See the class summary
    /// for how failure is handled.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<ComposedFeedSection>> ComposeAsync(
        PersonalizationContext context, CancellationToken cancellationToken = default);
}
