using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.KnowledgeAnalytics;

/// <summary>
/// One phrase people search for, counted per country.
/// <para>
/// <b>The most valuable table in this phase, and the cheapest.</b> A search with no results is the
/// platform being told exactly what knowledge is missing, by somebody who needed it — which is
/// better commissioning intelligence than any amount of guessing about what a diaspora wants.
/// <see cref="ResultCount"/> being zero on a phrase with a high <see cref="SearchCount"/> is a
/// guide that should be written this month.
/// </para>
/// <para>
/// <b>It is also the trending-topics source, and that is deliberate rather than convenient.</b> A
/// "trending topic" derived from tags tells you what has been written about; derived from searches
/// it tells you what people are looking for, and those two diverge most exactly when something
/// changes — a visa rule, an exchange rate, a school registration deadline. The divergence is the
/// signal.
/// </para>
/// <para>
/// <b>Phrases, never people.</b> No member identifier is recorded and none is accepted. The row
/// says "forty-one people in the Mauritius directory searched for work permit renewal", which is
/// everything an editor needs and nothing that could identify one of them.
/// </para>
/// </summary>
public sealed class KnowledgeSearchTerm : AggregateRoot<KnowledgeSearchTermId>, IAuditableEntity
{
    private KnowledgeSearchTerm(
        KnowledgeSearchTermId id,
        string countryCode,
        string term,
        DateTimeOffset now)
        : base(id)
    {
        CountryCode = countryCode;
        Term = term;
        SearchCount = 1;
        LastSearchedAt = now;
        CreatedAt = now;
    }

    private KnowledgeSearchTerm() { }

    /// <summary>
    /// Upper-case ISO 3166-1 alpha-2, or the empty string for a search that named no country.
    /// <para>
    /// Empty rather than null so the unique index over (country, term) works on every provider —
    /// a nullable column in a unique index treats every null as distinct on some of them, which
    /// would turn one global phrase into a new row on every search.
    /// </para>
    /// </summary>
    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>The normalized phrase: trimmed, lower-cased, whitespace collapsed.</summary>
    public string Term { get; private set; } = string.Empty;

    public long SearchCount { get; private set; }

    /// <summary>
    /// How many results the most recent search for this phrase returned.
    /// <para>
    /// The latest rather than an average, because the question it answers is "does this phrase find
    /// anything <em>now</em>" — and the moment somebody writes the missing guide, the answer should
    /// change on the next search rather than decay towards it over a month of averaging.
    /// </para>
    /// </summary>
    public int ResultCount { get; private set; }

    public DateTimeOffset LastSearchedAt { get; private set; }

    /// <summary>Whether this phrase currently finds nothing. The commissioning queue.</summary>
    public bool IsUnanswered => ResultCount == 0;

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    /// <summary>
    /// Starts counting a phrase, or returns null when it is not worth counting.
    /// <para>
    /// A single character, a blank, or a paste of somebody's whole question in four hundred
    /// characters are all noise that would bury the phrases an editor can act on.
    /// </para>
    /// </summary>
    public static KnowledgeSearchTerm? Start(
        KnowledgeSearchTermId id,
        string? countryCode,
        string? term,
        int resultCount,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);

        var normalized = Normalize(term);

        if (normalized is null)
        {
            return null;
        }

        var record = new KnowledgeSearchTerm(
            id, KnowledgeNormalization.Country(countryCode) ?? string.Empty, normalized, now);

        record.ResultCount = resultCount;

        return record;
    }

    public void Record(int resultCount, DateTimeOffset now)
    {
        SearchCount++;
        ResultCount = resultCount;
        LastSearchedAt = now;
    }

    /// <summary>
    /// The stored form of a phrase: trimmed, lower-cased, internal whitespace collapsed.
    /// <para>
    /// Public because the lookup has to normalize the same way the write did. Two spellings of one
    /// phrase are two rows that each look unimportant and are collectively the top search on the
    /// platform.
    /// </para>
    /// </summary>
    public static string? Normalize(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return null;
        }

        var collapsed = string.Join(
            ' ', term.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        collapsed = collapsed.ToLowerInvariant();

        return collapsed.Length is < 2 or > KnowledgeLimits.MaxSearchTerm ? null : collapsed;
    }

    /// <summary>Stamped by the shared persistence interceptor. Never called by a handler.</summary>
    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    /// <inheritdoc cref="SetCreated" />
    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
