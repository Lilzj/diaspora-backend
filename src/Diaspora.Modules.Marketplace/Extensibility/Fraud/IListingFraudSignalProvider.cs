namespace Diaspora.Modules.Marketplace.Extensibility.Fraud;

/// <summary>
/// Contributes a fraud or spam signal about a listing.
/// <para>
/// <b>The brief's "spam detection hooks", and the reason they are hooks rather than an
/// implementation.</b> Real fraud detection needs a model, a training set and a feedback loop, all
/// of which are somebody else's module. What B10 owes that module is a place to plug in that runs
/// at the right moment — before publication and on every report — and a vocabulary for what it
/// finds.
/// </para>
/// <para>
/// <b>A signal is advice, never a decision.</b> Providers return findings and the module decides
/// what to do with them: a severe finding holds the listing for review, a warning flags it into
/// the queue, and information is recorded and nothing else. No provider can take a listing down,
/// because a false positive that removed a member's listing silently would cost them a sale and
/// tell them nothing.
/// </para>
/// <para>
/// <b>Providers must be fast and must not throw.</b> They run inline on the publish path; the
/// assessor isolates each one, so a provider's outage costs its own signal and not the
/// publication.
/// </para>
/// </summary>
public interface IListingFraudSignalProvider
{
    /// <summary>Who is contributing. Recorded on every signal so a noisy provider is identifiable.</summary>
    string Source { get; }

    /// <summary>
    /// What this provider makes of the listing. An empty answer means "nothing to say", which is
    /// the ordinary case and must be cheap.
    /// </summary>
    Task<IReadOnlyList<FraudSignal>> EvaluateAsync(
        FraudEvaluationContext context, CancellationToken cancellationToken = default);
}
