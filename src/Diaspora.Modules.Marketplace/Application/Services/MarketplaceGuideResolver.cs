using Diaspora.Modules.Guides.Integration;
using Microsoft.Extensions.Logging;
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Marketplace.Application.Services;

/// <summary>
/// Hydrates the guides shown beside a listing — how to buy a used car safely, what an import duty
/// costs, how to check a serial number.
/// <para>
/// <b>This is the contextual link that makes the two modules worth having together.</b> A
/// second-hand vehicle listed in a country the buyer moved to last year is exactly the moment B8's
/// registration guide is worth reading, and putting it on the listing means they get it without
/// knowing the knowledge base exists.
/// </para>
/// <para>
/// The listing stores identifiers and a label; the guide's title and whether it is still published
/// are B8's answers at read time. A listing must never render a link to a guide that has since been
/// unpublished, and a copied title would do exactly that.
/// </para>
/// <para>
/// Reads fail soft. A listing taken down because the knowledge base was briefly unavailable would
/// be a dependency inverted, and a missing sidebar is a smaller loss than a missing page.
/// </para>
/// </summary>
public sealed class MarketplaceGuideResolver
{
    private readonly IGuideDirectory _guides;
    private readonly ILogger<MarketplaceGuideResolver> _logger;

    public MarketplaceGuideResolver(
        IGuideDirectory guides, ILogger<MarketplaceGuideResolver> logger)
    {
        _guides = guides;
        _logger = logger;
    }

    /// <summary>Summaries for the linked guides, keyed by identifier. Missing ones absent.</summary>
    public async Task<IReadOnlyDictionary<Guid, GuideSummary>> ResolveAsync(
        IReadOnlyCollection<Guid> guideIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(guideIds);

        var ids = guideIds.Where(id => id != Guid.Empty).Distinct().ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, GuideSummary>();
        }

        try
        {
            return await _guides.FindManyAsync([.. ids.Select(GuideId.From)], cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A knowledge-base outage costs a sidebar, not a page.
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Hydrating guide references from B8 failed.");

            return new Dictionary<Guid, GuideSummary>();
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Whether the guide exists and can be linked. Does not fail soft, for the same reason the
    /// provider check does not: a link to a guide that is not there is worse than a refused attach
    /// the seller can retry.
    /// </summary>
    public async Task<bool> ExistsAsync(
        Guid guideId, CancellationToken cancellationToken = default) =>
        guideId != Guid.Empty
        && await _guides.ExistsAsync(GuideId.From(guideId), cancellationToken);

    // ────────────────────────────────────────────────────────────────────────────────────────
    // Automatic suggestion is deliberately absent, exactly as it is in B9.
    //
    // MarketplaceListing.ReplaceAutomaticGuideReferences and ListingGuideReference.IsAutomatic
    // exist and are correct — the aggregate can hold platform-suggested links separately from a
    // seller's own choices, so refreshing suggestions never discards a deliberate one. Nothing
    // calls them yet, and that is on purpose.
    //
    // Suggesting "buying-safely guides for Germany" needs a country-scoped listing, and B8
    // publishes none: IGuideDirectory resolves guides by id and slug. Adding one would mean
    // editing B8, which is exactly the coupling this phase committed to avoid — and the
    // alternative, guessing from category names, would put wrong guides beside listings where
    // money changes hands, which is worse than putting none there.
    //
    // The right fix is a country-scoped read on B8's own published contract, made by B8. Until
    // then sellers attach guides themselves and the seam sits ready.
    // ────────────────────────────────────────────────────────────────────────────────────────
}
