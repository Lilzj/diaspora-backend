using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Reports;
using Diaspora.Modules.Housing.Extensibility.Fraud;
using Diaspora.Modules.Housing.Observability;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Housing.Application.Services;

/// <summary>
/// Runs every registered fraud provider over a listing about to be published.
/// <para>
/// <b>One provider's outage costs its own signal and never the publication.</b> Each is isolated,
/// because a heuristic that crashed and took a genuine landlord's listing down with it would be
/// worse than the fraud it was looking for.
/// </para>
/// <para>
/// <b>The context is assembled once and given to every provider.</b> A provider that had to fetch
/// the landlord's upheld report count itself would need a repository, and a seam that requires a
/// repository is a seam nobody outside this module can implement.
/// </para>
/// </summary>
public sealed class HousingFraudAssessor
{
    private readonly IEnumerable<IHousingFraudSignalProvider> _providers;
    private readonly IHousingReportRepository _reports;
    private readonly IHousingListingRepository _listings;
    private readonly IClock _clock;
    private readonly ILogger<HousingFraudAssessor> _logger;

    public HousingFraudAssessor(
        IEnumerable<IHousingFraudSignalProvider> providers,
        IHousingReportRepository reports,
        IHousingListingRepository listings,
        IClock clock,
        ILogger<HousingFraudAssessor> logger)
    {
        _providers = providers;
        _reports = reports;
        _listings = listings;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Assesses a listing. Never throws.</summary>
    public async Task<HousingFraudAssessment> AssessAsync(
        HousingListing listing,
        LandlordProfile landlord,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listing);
        ArgumentNullException.ThrowIfNull(landlord);

        var providers = _providers.ToList();

        if (providers.Count == 0)
        {
            return HousingFraudAssessment.Clear;
        }

        var context = new HousingFraudEvaluationContext
        {
            Listing = listing,
            Landlord = landlord,
            UpheldReportsAgainstLandlord =
                await _reports.CountUpheldForLandlordAsync(landlord.Id, cancellationToken),
            LandlordActiveListings =
                await _listings.CountActiveByLandlordAsync(landlord.Id, cancellationToken),
            Now = _clock.UtcNow
        };

        var signals = new List<HousingFraudSignal>();

        foreach (var provider in providers)
        {
            try
            {
                var raised = await provider.EvaluateAsync(context, cancellationToken);

                foreach (var signal in raised)
                {
                    signals.Add(signal with { Source = provider.Name });

                    HousingTelemetry.FraudSignalsRaised.Add(
                        1,
                        new KeyValuePair<string, object?>("severity", signal.Severity.ToString()),
                        new KeyValuePair<string, object?>("source", provider.Name),
                        new KeyValuePair<string, object?>("code", signal.Code));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // One provider's outage must not fail a publication. See the summary.
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Fraud provider {Provider} failed while assessing listing {ListingId}.",
                    provider.Name,
                    listing.Id.Value);
            }
#pragma warning restore CA1031
        }

        return new HousingFraudAssessment { Signals = signals };
    }
}
