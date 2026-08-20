using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.IntelligenceAnalytics;

/// <summary>
/// How much one country's official information is actually being used.
/// <para>
/// <b>Separate from <c>CountryKnowledgeAnalytics</c>, and the separation is editorial rather than
/// technical.</b> Knowledge analytics say whether the guides for a country are being read.
/// These say whether people saw the advisory in time. Folding them into one row would let a busy
/// month of guide reading hide a month in which nobody opened a single emergency number — which is
/// precisely the month somebody needs to know about.
/// </para>
/// <para>
/// <b>No concurrency token, and that is the same trade B8.1 makes for its counters.</b> A lost
/// increment is one view among thousands; a conflict on the most-read screen during an emergency
/// would be an outage. Counters clamp at zero and the row can be rebuilt from an authoritative
/// count, which is the escape hatch any counter maintained out of band has to have.
/// </para>
/// </summary>
public sealed class CountryIntelligenceAnalytics
    : AggregateRoot<CountryIntelligenceAnalyticsId>, IAuditableEntity
{
    private readonly List<IntelligenceCounter> _counters = [];

    private CountryIntelligenceAnalytics(
        CountryIntelligenceAnalyticsId id, string countryCode, DateTimeOffset createdAt)
        : base(id)
    {
        CountryCode = countryCode;
        CreatedAt = createdAt;
    }

    private CountryIntelligenceAnalytics() { }

    /// <summary>Upper-case ISO 3166-1 alpha-2. Unique — one row per country.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    public IReadOnlyList<IntelligenceCounter> Counters => _counters.AsReadOnly();

    /// <summary>When anything on this country's intelligence surface was last touched.</summary>
    public DateTimeOffset? LastActivityAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static CountryIntelligenceAnalytics Start(
        CountryIntelligenceAnalyticsId id, string? countryCode, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new CountryIntelligenceAnalytics(
            id, IntelligenceNormalization.Country(countryCode) ?? string.Empty, now);
    }

    /// <summary>The value of one counter, or zero when nothing has ever reported it.</summary>
    public long CountOf(string kind) =>
        _counters.FirstOrDefault(counter =>
            string.Equals(counter.Kind, kind, StringComparison.OrdinalIgnoreCase))?.Count ?? 0;

    /// <summary>Applies a delta to one counter, creating it on first sight. Returns the new value.</summary>
    public long Apply(string kind, long delta, DateTimeOffset at)
    {
        var counter = Resolve(kind);

        if (counter is null)
        {
            return 0;
        }

        LastActivityAt = at;

        return counter.Apply(delta, at);
    }

    /// <summary>Replaces one counter outright. For a rebuild from an authoritative count.</summary>
    public long Set(string kind, long count, DateTimeOffset at)
    {
        var counter = Resolve(kind);

        if (counter is null)
        {
            return 0;
        }

        LastActivityAt = at;

        return counter.Set(count, at);
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

    /// <summary>
    /// Finds a counter or starts one, refusing an unusable kind.
    /// <para>
    /// Returning null for a blank kind rather than throwing, because the caller is an analytics
    /// recorder on a read path: a malformed signal must cost a dropped count, never a failed page.
    /// </para>
    /// </summary>
    private IntelligenceCounter? Resolve(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return null;
        }

        var normalized = kind.Trim().ToLowerInvariant();

        var existing = _counters.FirstOrDefault(counter =>
            string.Equals(counter.Kind, normalized, StringComparison.Ordinal));

        if (existing is not null)
        {
            return existing;
        }

        var created = IntelligenceCounter.Start(normalized);

        _counters.Add(created);

        return created;
    }
}
