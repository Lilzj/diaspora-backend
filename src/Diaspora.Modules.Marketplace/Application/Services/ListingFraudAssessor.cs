using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Extensibility.Fraud;
using Diaspora.Modules.Marketplace.Observability;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Marketplace.Application.Services;

/// <summary>
/// Runs every registered fraud provider over a listing and collects what they find.
/// <para>
/// <b>The isolation is the whole job.</b> Providers run inline on the publish path; one that throws
/// or hangs must cost its own signal and not the seller's publication. Each is wrapped, each
/// failure is logged with its source, and the assessment carries whatever survived.
/// </para>
/// <para>
/// <b>The decision lives in <see cref="FraudAssessment"/>, not here.</b> Any severe finding holds
/// the listing; any warning flags it; everything else is recorded. Keeping that rule in the value
/// object means it is stated once and testable without a container.
/// </para>
/// <para>
/// A deployment with no providers registered gets <c>FraudAssessment.Clear</c> and pays nothing,
/// which is the state the platform ships in.
/// </para>
/// </summary>
public sealed class ListingFraudAssessor
{
    private readonly IEnumerable<IListingFraudSignalProvider> _providers;
    private readonly ILogger<ListingFraudAssessor> _logger;

    public ListingFraudAssessor(
        IEnumerable<IListingFraudSignalProvider> providers,
        ILogger<ListingFraudAssessor> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    /// <summary>Assesses a listing about to be published, or one that has just been reported.</summary>
    public async Task<FraudAssessment> AssessAsync(
        MarketplaceListing listing,
        SellerProfile seller,
        string trigger,
        int sellerActiveListings,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listing);
        ArgumentNullException.ThrowIfNull(seller);

        var providers = _providers.ToList();

        if (providers.Count == 0)
        {
            return FraudAssessment.Clear;
        }

        var context = new FraudEvaluationContext
        {
            ListingId = listing.Id.Value,
            SellerId = seller.Id.Value,
            Title = listing.Title.Value,
            Description = listing.Description.Body,
            CategoryKey = listing.CategoryKey,
            CountryCode = listing.Location.CountryCode,
            PriceAmount = listing.Price.Amount,
            CurrencyCode = listing.Price.CurrencyCode,
            MediaCount = listing.MediaReferences.Count,
            SellerAgeDays = Math.Max(0, (int)(now - seller.CreatedAt).TotalDays),
            SellerActiveListings = sellerActiveListings,
            IsSellerVerified = seller.IsVerified,
            Trigger = trigger
        };

        var signals = new List<FraudSignal>();

        foreach (var provider in providers)
        {
            signals.AddRange(await EvaluateSafelyAsync(provider, context, cancellationToken));
        }

        foreach (var signal in signals)
        {
            MarketplaceTelemetry.FraudSignalsRaised.Add(
                1,
                new KeyValuePair<string, object?>("severity", signal.Severity.ToString()),
                new KeyValuePair<string, object?>("code", signal.Code));
        }

        return new FraudAssessment { Signals = signals };
    }

    private async Task<IReadOnlyList<FraudSignal>> EvaluateSafelyAsync(
        IListingFraudSignalProvider provider,
        FraudEvaluationContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            return await provider.EvaluateAsync(context, cancellationToken) ?? [];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // One provider's failure must not fail a member's publication.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Fraud provider {Source} failed on listing {ListingId}. Its signal is missing from "
                + "this assessment.",
                provider.Source,
                context.ListingId);

            return [];
        }
#pragma warning restore CA1031
    }
}
