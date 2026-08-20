namespace Diaspora.Modules.Community.Extensibility.Moderation;

/// <summary>
/// Reviews content automatically.
/// <para>
/// <b>B6 ships no implementation of this</b>, and that is the same deliberate gap B5 left where
/// a virus scanner would go. A stub that answered clean would make an unmoderated deployment
/// look moderated, which is worse than an obviously missing capability — with nothing
/// registered, every review is recorded as unavailable, which is honest and visible in a metric.
/// </para>
/// <para>
/// Register one before opening a community to the public. The interface is deliberately shaped
/// so that a hosted classifier, a local model and a regular-expression blocklist all fit it.
/// </para>
/// </summary>
public interface IContentReviewProvider
{
    /// <summary>Recorded on every review it produces. Appears in appeal records for years.</summary>
    string Name { get; }

    int Order => 0;

    Task<ContentReviewResult> ReviewAsync(
        ContentReviewRequest request, CancellationToken cancellationToken = default);
}
