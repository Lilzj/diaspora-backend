using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.KnowledgeAnalytics;
using Diaspora.Modules.Guides.Extensibility.Analytics;
using Diaspora.Modules.Guides.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Guides.Infrastructure.Knowledge;

/// <summary>
/// Counts what people searched for, per country.
/// <para>
/// <b>A separate sink from the counters, because it can be turned off separately.</b> A deployment
/// whose policy is to retain nothing about what people looked for — even in aggregate, even with no
/// identity attached — should be able to hold that position without patching code, and without
/// losing the view counters that make the dashboard work. One option, one sink.
/// </para>
/// <para>
/// <b>Phrases, never people.</b> No member identifier is recorded and none would be accepted: the
/// signal contract does not carry one. What is stored is "forty-one searches for work permit renewal
/// in Mauritius, the last of which found nothing", which is everything an editorial team needs and
/// nothing that could identify anybody.
/// </para>
/// <para>
/// The unanswered phrases are the commissioning queue, and they are the most directly actionable
/// data this phase produces: every row is a guide somebody wanted and nobody has written, named in
/// the words they used to ask for it.
/// </para>
/// </summary>
public sealed class SearchTermAnalyticsSink : IKnowledgeAnalyticsSink
{
    private readonly IKnowledgeSearchTermRepository _terms;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IOptions<KnowledgeOptions> _options;

    public SearchTermAnalyticsSink(
        IKnowledgeSearchTermRepository terms,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IOptions<KnowledgeOptions> options)
    {
        _terms = terms;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _options = options;
    }

    public string Name => "search-terms";

    public async Task RecordAsync(
        KnowledgeAnalyticsSignal signal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);

        if (!_options.Value.RecordSearchTerms
            || signal.Kind != KnowledgeAnalyticsSignalKinds.Searched)
        {
            return;
        }

        var normalized = KnowledgeSearchTerm.Normalize(signal.Term);

        if (normalized is null)
        {
            // A single character, a blank, or somebody's whole question pasted in — noise that
            // would bury the phrases an editor can act on.
            return;
        }

        var country = signal.CountryCode ?? string.Empty;
        var resultCount = signal.ResultCount ?? 0;

        var existing = await _terms.GetAsync(country, normalized, cancellationToken);

        if (existing is null)
        {
            var created = KnowledgeSearchTerm.Start(
                KnowledgeSearchTermId.From(_guids.NewSequentialGuid()),
                country,
                normalized,
                resultCount,
                signal.At);

            if (created is null)
            {
                return;
            }

            await _terms.AddAsync(created, cancellationToken);
        }
        else
        {
            existing.Record(resultCount, signal.At);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
