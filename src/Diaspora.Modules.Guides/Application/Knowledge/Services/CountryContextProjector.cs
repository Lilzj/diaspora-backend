using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.EventHandlers;
using Diaspora.Modules.Guides.Domain.CountryContext;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Guides.Application.Knowledge.Services;

/// <summary>
/// Maintains the cross-vertical counter projection from other modules' published contracts.
/// <para>
/// <b>Every counter here is fed by an integration event and nothing else.</b> Not one line of this
/// module references the Jobs, Housing, Marketplace or Community Life assemblies — those modules
/// consume the Knowledge Directory, and a reference the other way would invert the dependency and
/// make every one of them queue behind a Guides release. An architecture test holds it to that.
/// </para>
/// <para>
/// <b>The counters are cumulative publications, and the labels say so.</b> Four of the five
/// verticals publish a country code on publication and none publishes one on withdrawal — a job
/// closing carries a job identifier, and resolving it to a country would mean querying the Jobs
/// module, which is exactly the reference this design forbids. Housing is the single exception: its
/// occupancy event carries a country, so its counter is a live one. The alternative designs were a
/// counter that drifts downward incorrectly, or no counter at all, and a cumulative number honestly
/// labelled is better than either.
/// </para>
/// <para>
/// <b>At-least-once delivery is assumed.</b> Increments are guarded on the first-publication flag
/// the contracts carry, so a redelivery of a republish does not double count; a redelivery of a
/// genuine first publication still can, which is why the counters clamp at zero and can be rebuilt
/// from an authoritative count.
/// </para>
/// </summary>
public sealed class CountryContextProjector
{
    private readonly ICountryContextSummaryRepository _summaries;
    private readonly KnowledgeIntegrationEventRelay _relay;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKnowledgeCache _cache;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CountryContextProjector(
        ICountryContextSummaryRepository summaries,
        KnowledgeIntegrationEventRelay relay,
        IUnitOfWork unitOfWork,
        IKnowledgeCache cache,
        IGuidGenerator guids,
        IClock clock)
    {
        _summaries = summaries;
        _relay = relay;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _guids = guids;
        _clock = clock;
    }

    /// <summary>
    /// Applies a delta to one counter for one country.
    /// <para>
    /// Saves its own work: an integration event handler runs in its own scope, after the producer's
    /// transaction committed, and nothing downstream is going to save for it.
    /// </para>
    /// </summary>
    public async Task ApplyAsync(
        string? countryCode,
        CountryContextKind kind,
        int delta,
        CancellationToken cancellationToken = default)
    {
        var country = KnowledgeNormalization.Country(countryCode);

        if (country is null || delta == 0)
        {
            // A vertical that published no country — Providers may — is simply not counted. There
            // is no sensible country to attribute it to, and attributing it to a wrong one would be
            // worse than a slightly low number.
            return;
        }

        var summary = await _summaries.GetAsync(country, cancellationToken);

        if (summary is null)
        {
            summary = CountryContextSummary.Start(
                CountryContextSummaryId.From(_guids.NewSequentialGuid()),
                country,
                _clock.UtcNow);

            await _summaries.AddAsync(summary, cancellationToken);
        }

        var count = summary.Apply(kind, delta, _clock.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The dashboard's numbers moved, so anything cached for the country is now wrong.
        await _cache.InvalidateCountryAsync(country, cancellationToken);

        await _relay.PublishDashboardChangedAsync(
            country,
            CountryContextSummary.NameOf(kind),
            count,
            _clock.UtcNow,
            cancellationToken);
    }
}
