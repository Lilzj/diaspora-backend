using Diaspora.Contracts.Events;
using Diaspora.Contracts.Location.V1.Events;
using Diaspora.Modules.Automation.Domain.Catalog;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Automation;

/// <summary>
/// The four triggers still unavailable, and the facts about the contracts assembly their reasons
/// rest on — re-audited 21 Oct 2026.
/// <para>
/// <b>Each reason is a claim that a contract is missing, and a claim like that goes stale silently.</b>
/// This platform has twice marked a trigger unreachable while the event that would drive it was
/// already published (<c>creator.challengeCompleted</c>, the Wave 12 lead and enquiry triggers). So
/// the premise is asserted against the compiled contracts rather than written down once: the day
/// somebody publishes a per-join community event, or gives an event a community, the matching test
/// here fails and says which trigger to wire.
/// </para>
/// </summary>
public sealed class UnwiredTriggerPremiseTests
{
    private static readonly IReadOnlyList<Type> IntegrationEvents =
    [
        .. typeof(IntegrationEvent).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(IntegrationEvent).IsAssignableFrom(type))
    ];

    [Theory]
    [InlineData("community.memberJoined")]
    [InlineData("community.eventCreated")]
    [InlineData("location.enteredArea")]
    [InlineData("location.countryChanged")]
    public void These_four_are_the_only_unavailable_triggers_and_each_says_why(string key)
    {
        AutomationCatalog.Triggers.Values
            .Where(trigger => !trigger.IsAvailable)
            .Select(trigger => trigger.Key)
            .Should().BeEquivalentTo(
                ["community.memberJoined", "community.eventCreated", "location.enteredArea", "location.countryChanged"]);

        var trigger = AutomationCatalog.FindTrigger(key)!;

        trigger.IsAvailable.Should().BeFalse();
        trigger.SourceEventName.Should().BeNull("an unavailable trigger names no contract it could not consume");
        trigger.UnavailableReason.Should().NotBeNullOrWhiteSpace();
        trigger.UnavailableReason.Should().NotBe(
            "The owning module does not publish a contract for this yet, so the platform cannot observe it.",
            "a generic reason sends the next reader on the same search; this one names what is missing");
    }

    [Fact]
    public void No_contract_announces_a_single_community_join()
    {
        IntegrationEvents
            .Where(type => type.Namespace?.Contains(".CommunityProgramme.", StringComparison.Ordinal) == true
                || type.Namespace?.Contains(".Community.", StringComparison.Ordinal) == true)
            .Where(type => type.Name.Contains("Join", StringComparison.Ordinal)
                || (type.Name.Contains("Member", StringComparison.Ordinal)
                    && !type.Name.Contains("Programme", StringComparison.Ordinal)))
            .Should().BeEmpty(
                "if CommunityProgramme now publishes a join, wire community.memberJoined to it — "
                + "CommunityMemberJoinedReason is stale");
    }

    [Fact]
    public void No_event_contract_says_which_community_it_belongs_to()
    {
        IntegrationEvents
            .Where(type => type.Namespace?.EndsWith(".CommunityLife.V1.Events", StringComparison.Ordinal) == true)
            .Where(type => type.GetProperty("CommunityId") is not null)
            .Should().BeEmpty(
                "if an event now carries its community, wire community.eventCreated to it — "
                + "CommunityEventCreatedReason is stale");
    }

    [Fact]
    public void Location_publishes_the_country_left_and_not_the_country_entered()
    {
        typeof(CountryResidenceEndedIntegrationEvent).GetProperties()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
                [
                    "UserId", "CountryCode", "DepartedOn",
                    .. typeof(IntegrationEvent).GetProperties().Select(property => property.Name)
                ],
                "a departure carries the country left; location.countryChanged's country condition is "
                + "about the country the member is in");

        IntegrationEvents
            .Where(type => type.Namespace?.EndsWith(".Location.V1.Events", StringComparison.Ordinal) == true)
            .Select(type => type.Name)
            .Should().Equal(
                ["CountryResidenceEndedIntegrationEvent"],
                "if Location now publishes a residence being declared, location.countryChanged can be "
                + "observed by the server — revisit its reason");
    }
}
