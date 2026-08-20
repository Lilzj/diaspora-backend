using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diaspora.Contracts.Automation.V1.Events;
using Diaspora.Modules.Automation.Catalog;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.Modules.Automation.Domain.SavedSearches;
using Diaspora.Modules.Automation.Observability;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Automation.Application.Services;

/// <summary>One published item, reduced to the fields a saved search can compare.</summary>
/// <param name="ItemId">The publishing module's own identifier for it.</param>
/// <param name="Title">What the alert says was found.</param>
/// <param name="Fields">
/// The item's public, filterable attributes — country, category, city, price. Keyed on the same names
/// the module's own list route accepts, which is what makes one matcher serve five subjects.
/// </param>
public readonly record struct SavedSearchCandidate(
    Guid ItemId,
    string Title,
    IReadOnlyDictionary<string, string?> Fields,
    string? DeepLink);

/// <summary>
/// One implementation shared by jobs, marketplace, housing, providers and events.
/// <para>
/// <b>§30 asks for exactly one, and this is the reason the criteria are a flat bag.</b> Five typed
/// shapes would be five matchers to keep in step and five places to add a filter; a flat map of the
/// filters those modules' own list routes already accept is one comparison loop over whatever the
/// consumer put in the candidate.
/// </para>
/// <para>
/// <b>"Once, ever" is the single most visible correctness property of the feature.</b> A member must be
/// told about a listing once even if it is republished, edited or re-indexed — so the barrier is a
/// durable row per <c>(SavedSearchId, MatchedItemId)</c> with a unique index, and the insert is what
/// fails. A de-duplication window would not do it: a listing republished a month later would notify
/// again.
/// </para>
/// <para>
/// <b>A saved search must never surface an item the member could not have found by searching.</b> The
/// candidates this matcher sees come from publication contracts only — a published job, a published
/// listing — so a private or unpublished item never reaches it. That is the boundary rather than a
/// visibility check here, because a visibility check here would be a second, divergent copy of five
/// modules' rules.
/// </para>
/// </summary>
public sealed class SavedSearchMatcher
{
    private readonly ISavedSearchRepository _searches;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IClock _clock;
    private readonly ILogger<SavedSearchMatcher> _logger;

    public SavedSearchMatcher(
        ISavedSearchRepository searches,
        IIntegrationEventPublisher publisher,
        IClock clock,
        ILogger<SavedSearchMatcher> logger)
    {
        _searches = searches;
        _publisher = publisher;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Matches one published item against every saved search watching its subject.
    /// </summary>
    public async Task MatchAsync(
        SavedSearchSubject subject,
        SavedSearchCandidate candidate,
        string change,
        CancellationToken cancellationToken = default)
    {
        var searches = await _searches.ListWatchingAsync(subject, cancellationToken);

        if (searches.Count == 0)
        {
            return;
        }

        var now = _clock.UtcNow;
        var notified = 0;

        foreach (var search in searches)
        {
            if (!search.Watches(change) || !Matches(search, candidate))
            {
                continue;
            }

            if (await _searches.HasMatchedAsync(search.Id, candidate.ItemId, cancellationToken))
            {
                continue;
            }

            _searches.AddMatch(SavedSearchMatch.Record(search.Id, candidate.ItemId, change, now));
            search.RecordMatch(now);

            if (search.AlertFrequency != SavedSearchAlertFrequency.Off)
            {
                // Publishes a fact and stops. B13 decides the channel, the digest window and the
                // recipient's quiet hours — including whether "instant" actually arrives instantly,
                // which is the member's own preference and not this module's decision.
                await _publisher.PublishAsync(
                    new AutomationDeliveryRequestedIntegrationEvent
                    {
                        AutomationId = search.AutomationId?.Value ?? Guid.Empty,
                        RunId = Guid.Empty,
                        ActionId = Guid.Empty,
                        RecipientUserId = search.MemberId.Value,
                        Audience = AutomationNotificationKeys.SavedSearchAudience,
                        AutomationName = search.Name,
                        Summary = candidate.Title,
                        SubjectType = subject.ToString().ToLowerInvariant(),
                        SubjectId = candidate.ItemId.ToString("D"),
                        SubjectTitle = candidate.Title,
                        OccurredAt = now,

                        // A saved-search alert has no run to settle, so no receipt is asked for.
                        // Asking for one that nothing would read is outbox traffic with no reader.
                        RequiresSettlementReceipt = false
                    },
                    cancellationToken);

                notified++;
            }
        }

        if (notified > 0)
        {
            AutomationTelemetry.Actions.Add(
                notified,
                new KeyValuePair<string, object?>("action", "savedSearch.alert"),
                new KeyValuePair<string, object?>("outcome", "Requested"));
        }

        await _searches.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Whether one item satisfies one saved search's criteria.
    /// <para>
    /// Every criterion must match. A criterion whose field the candidate does not carry does not
    /// match — fail closed, because a saved search that matched everything it could not check would
    /// notify a member about every listing on the platform.
    /// </para>
    /// <para>
    /// Three comparison shapes and no more: equality, membership in a comma-separated set, and a
    /// numeric bound for keys named <c>minX</c> or <c>maxX</c>. There is no operator syntax in a
    /// criteria bag and nothing here parses one.
    /// </para>
    /// </summary>
    private static bool Matches(SavedSearch search, SavedSearchCandidate candidate)
    {
        Dictionary<string, string?> criteria;

        try
        {
            criteria = Read(search.Criteria);
        }
        catch (JsonException)
        {
            return false;
        }

        foreach (var (key, expected) in criteria)
        {
            if (string.IsNullOrWhiteSpace(expected))
            {
                continue;
            }

            if (key.StartsWith("min", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("max", StringComparison.OrdinalIgnoreCase))
            {
                if (!Bound(key, expected, candidate.Fields))
                {
                    return false;
                }

                continue;
            }

            if (!candidate.Fields.TryGetValue(key, out var actual) || string.IsNullOrWhiteSpace(actual))
            {
                return false;
            }

            var options = expected.Split(
                ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (!options.Any(option => string.Equals(option, actual, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Bound(
        string key, string expected, IReadOnlyDictionary<string, string?> fields)
    {
        var field = key[3..];

        if (field.Length == 0)
        {
            return false;
        }

        field = char.ToLowerInvariant(field[0]) + field[1..];

        if (!fields.TryGetValue(field, out var raw) ||
            !decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var actual) ||
            !decimal.TryParse(expected, NumberStyles.Number, CultureInfo.InvariantCulture, out var limit))
        {
            return false;
        }

        return key.StartsWith("min", StringComparison.OrdinalIgnoreCase)
            ? actual >= limit
            : actual <= limit;
    }

    /// <summary>
    /// The de-duplication fingerprint: criteria with keys ordered and values trimmed, hashed.
    /// <para>
    /// Saving the same search twice returns the existing one rather than creating a duplicate that
    /// would double every alert. Ordered first, because two clients serialising the same filters in a
    /// different key order must produce the same fingerprint.
    /// </para>
    /// </summary>
    public static string Fingerprint(string criteria)
    {
        Dictionary<string, string?> values;

        try
        {
            values = Read(criteria);
        }
        catch (JsonException)
        {
            values = [];
        }

        var canonical = string.Join(
            '&',
            values
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => $"{entry.Key.ToLowerInvariant()}={entry.Value!.Trim().ToLowerInvariant()}"));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..48];
    }

    private static Dictionary<string, string?> Read(string json)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(json))
        {
            return values;
        }

        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return values;
        }

        foreach (var property in document.RootElement.EnumerateObject())
        {
            values[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Array => string.Join(
                    ',', property.Value.EnumerateArray().Select(element => element.ToString())),
                _ => null
            };
        }

        return values;
    }
}
