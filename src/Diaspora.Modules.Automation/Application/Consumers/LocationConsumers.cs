using Diaspora.Contracts.Location.V1.Events;
using Diaspora.Modules.Automation.Application.Engine;

namespace Diaspora.Modules.Automation.Application.Consumers;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// The two location triggers — 21 Oct 2026.
//
// Both were device-local and refused by the server: the device could see a country change in the
// foreground, and nothing on the server could see either. Location now publishes the country a member
// moved into, and — for a member who opted in — the area the app reported them entering while open.
// Both concern exactly one person, the member, and both reach nobody else: every action a User-owned
// automation may take is personal.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A member's residence started in a country — <c>location.countryChanged</c>.
/// <para>
/// The country in <c>country</c> is the one the member is in now, which is what the condition means;
/// the departure contract names the country left and is deliberately not consumed here.
/// </para>
/// </summary>
public sealed class CountryResidenceStartedAutomationConsumer
    : AutomationEventConsumer<CountryResidenceStartedIntegrationEvent>
{
    public CountryResidenceStartedAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        CountryResidenceStartedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["country"] = integrationEvent.CountryCode;

        yield return Context(
            "location.countryChanged",
            integrationEvent.EventId,
            Owners(integrationEvent.UserId),
            $"You now live in {integrationEvent.CountryName}.",
            integrationEvent.OccurredAt,
            values: values);
    }
}

/// <summary>
/// A member entered an area while the app was open — <c>location.enteredArea</c>.
/// <para>
/// <b><c>neighbourhood</c> carries every area newly entered, comma-separated</b> — the most specific
/// first, then each enclosing town or district that is new too. The evaluator reads a neighbourhood
/// value as a set, so "equals Peckham" and "includes London" both match on the way in, and moving
/// between two of London's neighbourhoods does not re-enter London. No coordinate reaches here: the
/// contract carries areas only.
/// </para>
/// </summary>
public sealed class MemberEnteredAreaAutomationConsumer
    : AutomationEventConsumer<MemberEnteredAreaIntegrationEvent>
{
    public MemberEnteredAreaAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        MemberEnteredAreaIntegrationEvent integrationEvent)
    {
        var entered = integrationEvent.EnteredAreaIds.Count > 0
            ? integrationEvent.EnteredAreaIds
            : [integrationEvent.AreaId];

        var values = Values();
        values["neighbourhood"] = string.Join(',', entered.Select(id => id.ToString("D")));
        values["country"] = integrationEvent.CountryCode;

        yield return Context(
            "location.enteredArea",
            integrationEvent.EventId,
            Owners(integrationEvent.UserId),
            $"You arrived in {integrationEvent.AreaName}.",
            integrationEvent.OccurredAt,
            values: values);
    }
}
