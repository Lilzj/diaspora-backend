using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Automation.Domain.SavedSearches;

/// <summary>
/// A saved search, and the alert configured on it.
/// <para>
/// <b>One implementation shared by five subjects.</b> §30 asks for exactly that, and the criteria are
/// therefore a flat bag of the filters those modules' own list routes already accept rather than five
/// typed shapes — a per-module type would be five places to add a filter and five matchers to keep in
/// step.
/// </para>
/// <para>
/// <b>The criteria are personal.</b> A housing search states a budget and a neighbourhood; a job
/// search states a career intention. They are readable only by the member, they never enter analytics
/// — the vocabulary carries the subject and a criteria <i>count</i>, never the criteria — and the
/// normalised form below exists for de-duplication rather than for reading.
/// </para>
/// </summary>
public sealed class SavedSearch : AggregateRoot<SavedSearchId>
{
    private SavedSearch() { }

    private SavedSearch(SavedSearchId id) : base(id) { }

    public UserId MemberId { get; private set; } = UserId.Empty;

    public SavedSearchSubject Subject { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>The query as the module's own list screen would send it, serialized.</summary>
    public string Criteria { get; private set; } = "{}";

    /// <summary>
    /// The criteria with keys ordered and values trimmed, hashed.
    /// <para>
    /// The idempotency subject with the member and the subject: saving the same search twice returns
    /// the existing one rather than creating a duplicate that would double every alert. Hashed rather
    /// than stored whole because it is compared and never read.
    /// </para>
    /// </summary>
    public string CriteriaFingerprint { get; private set; } = string.Empty;

    /// <summary>What the member typed, for display.</summary>
    public string QuerySummary { get; private set; } = string.Empty;

    public bool AlertsEnabled { get; private set; }

    public SavedSearchAlertFrequency AlertFrequency { get; private set; }

    /// <summary>Which changes are worth telling them about, delimited.</summary>
    public string WatchFor { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Server-owned. Null when no evaluation has ever matched.</summary>
    public DateTimeOffset? LastMatchedAt { get; private set; }

    public int NewMatchCount { get; private set; }

    /// <summary>The automation this search's alert is delivered by, when the member made one.</summary>
    public AutomationId? AutomationId { get; private set; }

    public int Revision { get; private set; }

    public static SavedSearch Create(
        UserId memberId,
        SavedSearchSubject subject,
        string name,
        string criteria,
        string criteriaFingerprint,
        string querySummary,
        bool alertsEnabled,
        SavedSearchAlertFrequency alertFrequency,
        IEnumerable<string> watchFor,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(memberId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new SavedSearch(SavedSearchId.New())
        {
            MemberId = memberId,
            Subject = subject,
            Name = Cap(name, 120),
            Criteria = string.IsNullOrWhiteSpace(criteria) ? "{}" : criteria,
            CriteriaFingerprint = criteriaFingerprint,
            QuerySummary = Cap(querySummary, 200),
            AlertsEnabled = alertsEnabled,
            AlertFrequency = alertFrequency,
            WatchFor = string.Join(',', watchFor.Distinct(StringComparer.Ordinal)),
            CreatedAt = now,
            UpdatedAt = now,
            Revision = 1
        };
    }

    public void UpdateAlerting(
        string? name,
        bool? alertsEnabled,
        SavedSearchAlertFrequency? frequency,
        IEnumerable<string>? watchFor,
        DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = Cap(name, 120);
        }

        if (alertsEnabled is { } enabled)
        {
            AlertsEnabled = enabled;
        }

        if (frequency is { } value)
        {
            AlertFrequency = value;
        }

        if (watchFor is not null)
        {
            WatchFor = string.Join(',', watchFor.Distinct(StringComparer.Ordinal));
        }

        UpdatedAt = now;
        Revision++;
    }

    public void LinkAutomation(AutomationId automationId, DateTimeOffset now)
    {
        AutomationId = automationId;
        UpdatedAt = now;
        Revision++;
    }

    public void RecordMatch(DateTimeOffset at)
    {
        LastMatchedAt = at;
        NewMatchCount++;
        Revision++;
    }

    public IReadOnlyList<string> WatchForList() =>
        string.IsNullOrWhiteSpace(WatchFor)
            ? []
            : WatchFor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Whether this search wants to hear about a change of this kind.
    /// <para>
    /// An empty watch list means new matches only, which is what a member who saved a search without
    /// opening the alert settings meant.
    /// </para>
    /// </summary>
    public bool Watches(string change)
    {
        var list = WatchForList();
        return list.Count == 0
            ? string.Equals(change, "newMatch", StringComparison.Ordinal)
            : list.Contains(change, StringComparer.Ordinal);
    }

    private static string Cap(string value, int max) =>
        value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];
}

/// <summary>
/// One item a saved search has already matched.
/// <para>
/// <b>"Once, ever" is this table.</b> The register calls it "the single most visible correctness
/// property of the feature": a member must be told about a listing once even if it is republished,
/// edited or re-indexed. A de-duplication window would not do it — a listing republished a month
/// later would notify again — so the barrier is a durable row with a unique index on
/// <c>(SavedSearchId, MatchedItemId)</c> and the insert is what fails, not a prior read.
/// </para>
/// </summary>
public sealed class SavedSearchMatch : Entity<Guid>
{
    private SavedSearchMatch() { }

    private SavedSearchMatch(Guid id) : base(id) { }

    public SavedSearchId SavedSearchId { get; private set; } = null!;

    /// <summary>The published item's own identifier, as the publishing module names it.</summary>
    public Guid MatchedItemId { get; private set; }

    /// <summary>Which change produced it — <c>newMatch</c>, <c>availabilityChange</c>.</summary>
    public string Change { get; private set; } = string.Empty;

    public DateTimeOffset MatchedAt { get; private set; }

    /// <summary>Whether an alert was raised, so a digest can find what it has not yet sent.</summary>
    public bool Notified { get; private set; }

    public static SavedSearchMatch Record(
        SavedSearchId savedSearchId, Guid matchedItemId, string change, DateTimeOffset at) =>
        new(Guid.NewGuid())
        {
            SavedSearchId = savedSearchId,
            MatchedItemId = matchedItemId,
            Change = change,
            MatchedAt = at
        };

    public void MarkNotified() => Notified = true;
}
