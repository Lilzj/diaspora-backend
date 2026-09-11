using System.Text.Json;
using Diaspora.Modules.Automation.Contracts.V1.Requests;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Automation;

/// <summary>
/// The body the app sends, bound the way the write routes bind it — 11 Sep 2026.
/// <para>
/// <b>The routes had never read a rule the app's builder wrote.</b> On the production APK every
/// save and every test was refused at model binding, before a handler ran: the builder named its
/// rows <c>c1-country</c> and <c>a1-notify.self</c>, and <see cref="AutomationConditionRequest.Id"/>
/// is a <c>Guid?</c>. With the names removed the body bound — and lost the member's zone, because the
/// app sent the schedule in the shape these routes <i>answer</i> with, a nested
/// <c>timezone</c> object, which the request record has no field for and so ignores.
/// </para>
/// <para>
/// The app's half of this join is <c>src/lib/__tests__/automation-write-contract.test.ts</c>, which
/// checks what its builder writes against these records field by field. This half pins the other
/// end: the bodies below are copied from that client's output, and each must bind with nothing lost.
/// A change to either end has to be carried to the other, or one of the two fails.
/// </para>
/// </summary>
public sealed class AutomationWriteBodyBindingTests
{
    /// <summary>What minimal-API endpoints bind with. The MVC options in Program.cs do not reach them.</summary>
    private static readonly JsonSerializerOptions RouteOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// "When an event I am going to is starting soon, if the country is Mauritius, tell me" — the rule
    /// built on the device, as the app now writes it.
    /// </summary>
    private const string DeviceRule = """
        {
          "name": "",
          "description": "",
          "scope": {
            "ownerKind": "user",
            "ownerId": "0dc73f0c-7efa-4b60-8706-7a60eacace65",
            "communityIds": [],
            "isCrossCommunity": false,
            "countryCodes": [],
            "neighbourhoodIds": []
          },
          "source": "builder",
          "templateId": null,
          "schedule": {
            "frequency": "daily",
            "timezoneMode": "device",
            "timeZoneId": "Africa/Lagos",
            "hour": 9,
            "minute": 0,
            "daysOfWeek": [],
            "dayOfMonth": null,
            "startsAt": null,
            "endsAt": null,
            "maxExecutions": null,
            "eventOffsetMinutes": null,
            "eventOffsetAnchor": null,
            "customDescription": null
          },
          "trigger": { "key": "event.startingSoon", "parameters": "{}" },
          "conditions": [
            { "id": null, "key": "country", "operator": "equals", "value": "MU", "secondaryValue": null }
          ],
          "actions": [ { "id": null, "key": "notify.self", "parameters": "{}" } ],
          "sampleSubjectId": "01a083f8-a050-7465-90c6-3ba0f0adfb3e",
          "idempotencyKey": "automation-create-1"
        }
        """;

    /// <summary>"Remind me a day before", as the app writes an event-relative schedule.</summary>
    private const string EventRelativeSchedule = """
        {
          "frequency": "eventRelative",
          "timezoneMode": "event",
          "timeZoneId": "Indian/Mauritius",
          "hour": null,
          "minute": null,
          "daysOfWeek": [],
          "dayOfMonth": null,
          "startsAt": null,
          "endsAt": null,
          "maxExecutions": null,
          "eventOffsetMinutes": 1440,
          "eventOffsetAnchor": "start",
          "customDescription": null
        }
        """;

    [Fact]
    public void The_test_route_reads_the_rule_and_the_subject_it_is_checked_against()
    {
        var request = JsonSerializer.Deserialize<SimulateAutomationRequest>(DeviceRule, RouteOptions)!;

        request.Trigger!.Key.Should().Be("event.startingSoon");
        request.Conditions.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Id = (Guid?)null, Key = "country", Value = "MU" });
        request.Actions.Should().ContainSingle().Which.Key.Should().Be("notify.self");
        request.SampleSubjectId.Should().Be(Guid.Parse("01a083f8-a050-7465-90c6-3ba0f0adfb3e"));
    }

    [Fact]
    public void The_create_route_keeps_the_zone_the_member_chose()
    {
        var request = JsonSerializer.Deserialize<CreateAutomationRequest>(DeviceRule, RouteOptions)!;

        request.Schedule!.TimeZoneId.Should().Be(
            "Africa/Lagos",
            "a schedule saved without its zone cannot be resolved when it is due, and never fires");
        request.Schedule.TimezoneMode.Should().Be("device");
        request.Schedule.Frequency.Should().Be("daily");
        request.Schedule.Hour.Should().Be(9);
        request.Scope!.OwnerId.Should().Be(Guid.Parse("0dc73f0c-7efa-4b60-8706-7a60eacace65"));
        request.IdempotencyKey.Should().Be("automation-create-1");
    }

    [Fact]
    public void An_event_relative_schedule_keeps_how_long_before_the_event_it_goes()
    {
        var schedule = JsonSerializer.Deserialize<AutomationScheduleRequest>(
            EventRelativeSchedule, RouteOptions)!;

        schedule.EventOffsetMinutes.Should().Be(1440);
        schedule.EventOffsetAnchor.Should().Be("start");
        schedule.TimeZoneId.Should().Be("Indian/Mauritius");
        schedule.Frequency.Should().Be("eventRelative");
    }
}
