using System.Globalization;
using Diaspora.Contracts.Automation.V1.Extensibility;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;

namespace Diaspora.Modules.CommunityLife.Extensibility.Automation;

/// <summary>
/// What an event answers, when somebody is building an automation about it — Phase 5.
/// <para>
/// <b>The values here must be the ones this module's own automation consumer produces, not better
/// ones.</b> A preview derived independently would be a second definition of what a rule matches:
/// the builder would show a member a match and the real run would skip. So this reproduces
/// the <c>event.created</c> consumer's <c>Translate</c> key for key (since 21 Oct 2026 the
/// publication consumer's), including the part of it that is
/// arguably wrong — <c>dayOfWeek</c> and <c>timeOfDay</c> are taken from the stored instant rather
/// than from the event's own zone, so an event at 00:30 Monday in Lagos reads as Sunday 23:30. That
/// is a real defect and it belongs to the consumer; correcting it only here would make the two
/// disagree, which is worse than both being wrong in the same direction.
/// </para>
/// <para>
/// <b>Visibility is this module's, unchanged.</b> The same <c>RequireEventReadAsync</c> the event
/// endpoint calls decides whether there is an answer, so a private event stays private and a preview
/// cannot become a way to read one. A refusal returns null, and the automation platform renders that
/// as "Diaspora could not read that event" without learning why.
/// </para>
/// </summary>
public sealed class EventAutomationSubjectSource : IAutomationSubjectSource
{
    /// <summary>Kept as a literal: this module cannot reference the Automation module's catalogue.</summary>
    private const string EventKind = "event";

    private readonly ICommunityEventRepository _events;
    private readonly CommunityLifeAuthorizationService _authorization;

    public EventAutomationSubjectSource(
        ICommunityEventRepository events,
        CommunityLifeAuthorizationService authorization)
    {
        _events = events;
        _authorization = authorization;
    }

    public IReadOnlyList<string> Answers { get; } = [EventKind];

    public async Task<AutomationSubjectSnapshot?> ReadAsync(
        AutomationSubjectQuery request, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.SubjectKind, EventKind, StringComparison.Ordinal)
            || request.SubjectId == Guid.Empty)
        {
            return null;
        }

        // The gate below reads the ambient caller, and this seam is only ever invoked inside that
        // member's own request. Checked rather than assumed: a future background caller passing some
        // other member's identity would otherwise be authorized as whoever the scope happened to
        // hold, which is the shape of every accidental cross-member read.
        if (_authorization.CallerId != request.Member)
        {
            return null;
        }

        var communityEvent = await _events.GetAsync(
            CommunityEventId.From(request.SubjectId), cancellationToken);

        if (communityEvent is null || communityEvent.IsDeleted)
        {
            return null;
        }

        var readable = await _authorization.RequireEventReadAsync(communityEvent, cancellationToken);

        if (readable.IsFailure)
        {
            return null;
        }

        // Written one assignment at a time, in the same `values["key"] =` shape the consumers use,
        // so `AutomationSubjectSourceArchitectureTests` can read both sides with one parse. A
        // collection initializer here would have hidden every key from that sweep — which it did,
        // until the sweep failed on the first run and said so.
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        values["country"] = communityEvent.Location.CountryCode;
        values["category"] = communityEvent.CategoryKey;

        // Both, because the consumer sets both from the same field. `eventType` reading a category
        // key is the consumer's choice and this follows it rather than improving on it.
        values["eventType"] = communityEvent.CategoryKey;
        values["dayOfWeek"] = ((int)communityEvent.Schedule.StartsAt.DayOfWeek)
            .ToString(CultureInfo.InvariantCulture);
        values["timeOfDay"] = communityEvent.Schedule.StartsAt
            .ToString("HH:mm", CultureInfo.InvariantCulture);

        // Omitted rather than nulled where the event has none — an absent key and a null one both
        // fail closed, and only one of them cannot be read as "the answer is nothing".
        if (communityEvent.Location.NeighborhoodAreaId is { } areaId)
        {
            values["neighbourhood"] = areaId.ToString("D");
        }

        if (!string.IsNullOrWhiteSpace(communityEvent.Location.CityName))
        {
            values["city"] = communityEvent.Location.CityName;
        }

        return new AutomationSubjectSnapshot
        {
            SubjectKind = EventKind,
            SubjectId = request.SubjectId,
            Title = communityEvent.Title.Value,
            Moment = communityEvent.Schedule.StartsAt,
            TimeZoneId = communityEvent.Schedule.TimeZoneId,
            Values = values
        };
    }
}
