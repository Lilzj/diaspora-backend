using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.CountryContext;

/// <summary>
/// What else the platform holds for one country, in numbers.
/// <para>
/// <b>The one place B8.1 touches another vertical, and it does so without referencing one.</b>
/// Every counter here is maintained by a handler subscribed to a <em>published integration
/// contract</em> in <c>Diaspora.Contracts</c> — a job published, a property listed, an event
/// announced. Not one line of this module references the Jobs, Housing, Marketplace or Community
/// Life assemblies, and an architecture test holds it to that: those modules consume the Knowledge
/// Directory, and a reference the other way would invert the dependency and make every one of them
/// queue behind a Guides release.
/// </para>
/// <para>
/// <b>Counts, and nothing that could be mistaken for content.</b> A country dashboard says "142
/// jobs — browse them" and the browsing happens in the Jobs module against the Jobs module's own
/// data. If this row held titles, the dashboard would be rendering a cache of somebody else's
/// content with none of their visibility rules, moderation state or freshness.
/// </para>
/// <para>
/// <b>Eventually consistent, and honest about it.</b> Delivery is at-least-once and out of band,
/// so a counter can be a few seconds behind and, after a replay, a little high. Every counter
/// clamps at zero and the whole row can be rebuilt from an authoritative count, which is the
/// escape hatch a projection maintained from a message bus has to have.
/// </para>
/// </summary>
public sealed class CountryContextSummary
    : AggregateRoot<CountryContextSummaryId>, IAuditableEntity
{
    private readonly List<CountryContextCounter> _counters = [];

    private CountryContextSummary(
        CountryContextSummaryId id, string countryCode, DateTimeOffset createdAt)
        : base(id)
    {
        CountryCode = countryCode;
        CreatedAt = createdAt;
    }

    private CountryContextSummary() { }

    /// <summary>Upper-case ISO 3166-1 alpha-2. Unique — one row per country.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    public IReadOnlyList<CountryContextCounter> Counters => _counters.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static CountryContextSummary Start(
        CountryContextSummaryId id, string countryCode, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new CountryContextSummary(
            id, KnowledgeNormalization.Country(countryCode) ?? string.Empty, now);
    }

    /// <summary>The value of one counter, or zero when nothing has ever reported it.</summary>
    public int CountOf(CountryContextKind kind) => CountOf(NameOf(kind));

    /// <inheritdoc cref="CountOf(CountryContextKind)" />
    public int CountOf(string kind) =>
        _counters.FirstOrDefault(counter =>
            string.Equals(counter.Kind, kind, StringComparison.OrdinalIgnoreCase))?.Count ?? 0;

    /// <summary>Applies a delta to one counter, creating it on first sight. Returns the new value.</summary>
    public int Apply(CountryContextKind kind, int delta, DateTimeOffset at) =>
        Apply(NameOf(kind), delta, at);

    /// <inheritdoc cref="Apply(CountryContextKind, int, DateTimeOffset)" />
    public int Apply(string kind, int delta, DateTimeOffset at)
    {
        var counter = Resolve(kind);

        return counter is null ? 0 : counter.Apply(delta, at);
    }

    /// <summary>Replaces one counter from an authoritative count. The rebuild path.</summary>
    public int Set(CountryContextKind kind, int count, DateTimeOffset at) =>
        Set(NameOf(kind), count, at);

    /// <inheritdoc cref="Set(CountryContextKind, int, DateTimeOffset)" />
    public int Set(string kind, int count, DateTimeOffset at)
    {
        var counter = Resolve(kind);

        return counter is null ? 0 : counter.Set(count, at);
    }

    /// <summary>The wire name of a shipped counter kind: lower-case, and what the contract carries.</summary>
    public static string NameOf(CountryContextKind kind) => kind switch
    {
        CountryContextKind.Guides => "guides",
        CountryContextKind.Jobs => "jobs",
        CountryContextKind.Housing => "housing",
        CountryContextKind.Marketplace => "marketplace",
        CountryContextKind.Events => "events",
        CountryContextKind.Organizations => "organizations",
        CountryContextKind.Providers => "providers",
        _ => kind.ToString().ToLowerInvariant()
    };

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

    private CountryContextCounter? Resolve(string kind)
    {
        var normalized = KnowledgeNormalization.Key(kind);

        if (normalized is null)
        {
            return null;
        }

        var counter = _counters.FirstOrDefault(existing =>
            string.Equals(existing.Kind, normalized, StringComparison.Ordinal));

        if (counter is null)
        {
            counter = CountryContextCounter.Start(normalized);
            _counters.Add(counter);
        }

        return counter;
    }
}
