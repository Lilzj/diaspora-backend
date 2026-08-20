using Diaspora.Modules.Guides.Integration;
using Microsoft.Extensions.Logging;
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Jobs.Application.Services;

/// <summary>
/// Hydrates and suggests the guides shown beside a posting — the work-permit explainer, the
/// interview-tips piece, the country employment guide, the salary-negotiation walkthrough.
/// <para>
/// <b>This is the contextual link that makes the two modules worth having together.</b> A posting
/// in Germany that sponsors a visa is exactly the moment a candidate needs B8's German
/// work-permit guide, and putting it on the posting means they get it without knowing the
/// knowledge base exists.
/// </para>
/// <para>
/// The posting stores identifiers and a label; the guide's title, badge and whether it is still
/// published are B8's answers at read time. A posting must never render a link to a guide that
/// has since been unpublished, and a copied title would do exactly that.
/// </para>
/// <para>
/// Both reads fail soft. A job advert taken down because the knowledge base was briefly
/// unavailable would be a dependency inverted, and a missing sidebar is a smaller loss than a
/// missing page.
/// </para>
/// </summary>
public sealed class JobGuideResolver
{
    private readonly IGuideDirectory _guides;
    private readonly IGuideCatalog _catalog;
    private readonly ILogger<JobGuideResolver> _logger;

    public JobGuideResolver(
        IGuideDirectory guides, IGuideCatalog catalog, ILogger<JobGuideResolver> logger)
    {
        _guides = guides;
        _catalog = catalog;
        _logger = logger;
    }

    /// <summary>Summaries for the linked guides, keyed by identifier. Missing ones absent.</summary>
    public async Task<IReadOnlyDictionary<Guid, GuideSummary>> ResolveAsync(
        IReadOnlyCollection<Guid> guideIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(guideIds);

        if (guideIds.Count == 0)
        {
            return new Dictionary<Guid, GuideSummary>();
        }

        try
        {
            return await _guides.FindManyAsync(
                [.. guideIds.Select(GuideId.From)], cancellationToken);
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
    /// provider check does not: a link to a guide that is not there is worse than a refused
    /// attach the employer can retry.
    /// </summary>
    public async Task<bool> ExistsAsync(
        Guid guideId, CancellationToken cancellationToken = default) =>
        await _guides.ExistsAsync(GuideId.From(guideId), cancellationToken);

    /// <summary>
    /// The guide categories this deployment offers, for an employer choosing a link to attach.
    /// </summary>
    public async Task<IReadOnlyList<GuideCategoryDescriptor>> ListCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _catalog.ListCategoriesAsync(
                includeInactive: false, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A knowledge-base outage costs a picker, not a page.
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Listing guide categories from B8 failed.");

            return [];
        }
#pragma warning restore CA1031
    }

    // ────────────────────────────────────────────────────────────────────────────────────────
    // Automatic suggestion is deliberately absent.
    //
    // JobPosting.ReplaceAutomaticGuideReferences and JobGuideReference.IsAutomatic exist and are
    // correct — the aggregate can hold platform-suggested links separately from an employer's own
    // choices, so refreshing suggestions never discards a deliberate one. Nothing calls them yet,
    // and that is on purpose.
    //
    // Suggesting "work-permit guides for Germany" needs a country-scoped listing, and B8 publishes
    // none: IGuideDirectory resolves guides by id and slug, IGuideCatalog answers about categories.
    // Adding one would mean editing B8, which is exactly the coupling this phase committed to
    // avoid — and the alternative, guessing from category names, would put wrong guides beside job
    // adverts, which is worse than putting none there.
    //
    // The right fix is a country-scoped read on B8's own published contract, made by B8. Until
    // then employers attach guides themselves and the seam sits ready.
    // ────────────────────────────────────────────────────────────────────────────────────────
}
