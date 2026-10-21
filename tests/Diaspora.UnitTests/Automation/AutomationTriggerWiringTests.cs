using Diaspora.Contracts.Automation.V1.Extensibility;
using Diaspora.Contracts.Events;
using Diaspora.Modules.Automation.Domain.Catalog;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Events;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.Location.Domain.Events;
using Diaspora.Modules.Location.Domain.Experience;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;
using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.UnitTests.Automation;

/// <summary>
/// The four triggers that were unavailable until 21 Oct 2026, and the producing facts behind each.
/// <para>
/// <b>Replaces <c>UnwiredTriggerPremiseTests</c></b>, which asserted the contracts were missing and
/// was written to fail the day one appeared. All four appeared together: a per-join community
/// contract, a community on an event, a residence starting, and an area entered. What is pinned now
/// is the other direction — each is available, names a contract that exists, and the domain raises
/// the fact exactly when it should and at no other time.
/// </para>
/// </summary>
public sealed class AutomationTriggerWiringTests
{
    private static readonly Dictionary<string, Type> Contracts =
        typeof(IntegrationEvent).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(IntegrationEvent).IsAssignableFrom(type))
            .ToDictionary(type => type.FullName!, StringComparer.Ordinal);

    [Fact]
    public void No_trigger_in_the_catalogue_is_unavailable_any_more()
    {
        AutomationCatalog.Triggers.Values
            .Where(trigger => !trigger.IsAvailable)
            .Select(trigger => trigger.Key)
            .Should().BeEmpty("the last four were wired on 21 Oct 2026");
    }

    [Theory]
    [InlineData("community.memberJoined")]
    [InlineData("community.eventCreated")]
    [InlineData("location.enteredArea")]
    [InlineData("location.countryChanged")]
    public void Each_names_a_contract_that_exists(string key)
    {
        var trigger = AutomationCatalog.FindTrigger(key)!;

        trigger.IsAvailable.Should().BeTrue();
        trigger.UnavailableReason.Should().BeNull();
        trigger.SourceEventName.Should().NotBeNull();
        Contracts.Should().ContainKey(trigger.SourceEventName!);
    }

    [Fact]
    public void An_event_contract_says_which_community_it_was_posted_into()
    {
        foreach (var name in new[]
        {
            "Diaspora.Contracts.CommunityLife.V1.Events.EventCreatedIntegrationEvent",
            "Diaspora.Contracts.CommunityLife.V1.Events.EventPublishedIntegrationEvent"
        })
        {
            Contracts[name].GetProperty("CommunityId")!.PropertyType.Should().Be<Guid?>();
        }
    }

    /// <summary>
    /// 21 Oct 2026: <c>event.created</c> fired on the draft for every member's automation. It is the
    /// first publication now, and every contract an event trigger reads says whether a stranger could
    /// have found the event — the flag that decides whether anybody but its host hears about it.
    /// </summary>
    [Fact]
    public void Event_created_reads_the_publication_and_every_event_trigger_contract_carries_its_audience()
    {
        AutomationCatalog.FindTrigger("event.created")!.SourceEventName
            .Should().Be("Diaspora.Contracts.CommunityLife.V1.Events.EventPublishedIntegrationEvent");

        foreach (var name in new[]
        {
            "Diaspora.Contracts.CommunityLife.V1.Events.EventPublishedIntegrationEvent",
            "Diaspora.Contracts.CommunityLife.V1.Events.EventUpdatedIntegrationEvent",
            "Diaspora.Contracts.CommunityLife.V1.Events.EventCancelledIntegrationEvent",
            "Diaspora.Contracts.CommunityLife.V1.Events.EventOccurrenceRescheduledIntegrationEvent"
        })
        {
            Contracts[name].GetProperty("IsListedPublicly")!.PropertyType.Should().Be<bool>();
        }
    }

    [Fact]
    public void Location_contracts_carry_no_coordinate()
    {
        foreach (var name in new[]
        {
            "Diaspora.Contracts.Location.V1.Events.MemberEnteredAreaIntegrationEvent",
            "Diaspora.Contracts.Location.V1.Events.CountryResidenceStartedIntegrationEvent"
        })
        {
            Contracts[name].GetProperties()
                .Select(property => property.Name)
                .Should().NotContain(
                    ["Latitude", "Longitude", "AccuracyMetres"],
                    "a presence report is resolved to an area and the point is discarded");
        }
    }

    // ── community.memberJoined: the transition, and only the transition ──────────────────────────

    [Fact]
    public void A_direct_join_raises_one_join_with_the_members_visibility()
    {
        var community = Opened(requiresApproval: false);
        var membership = Following(community, CommunityMembershipVisibility.Private);

        membership.Join(community, community.RequiresApproval, DateTimeOffset.UtcNow)
            .IsSuccess.Should().BeTrue();

        var joined = membership.DomainEvents.OfType<CommunityMemberJoinedDomainEvent>()
            .Should().ContainSingle().Subject;

        joined.Route.Should().Be(CommunityJoinRoute.Direct);
        joined.Visibility.Should().Be(CommunityMembershipVisibility.Private);
        joined.MemberId.Should().Be(membership.MemberId);
        joined.CommunityId.Should().Be(community.Id);
    }

    [Fact]
    public void A_request_raises_nothing_until_it_is_approved()
    {
        var community = Opened(requiresApproval: true);
        var membership = Following(community, CommunityMembershipVisibility.Public);

        membership.Join(community, community.RequiresApproval, DateTimeOffset.UtcNow)
            .IsSuccess.Should().BeTrue();

        membership.DomainEvents.OfType<CommunityMemberJoinedDomainEvent>()
            .Should().BeEmpty("a pending request is not a member");

        membership.Approve(UserId.New(), DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();

        membership.DomainEvents.OfType<CommunityMemberJoinedDomainEvent>()
            .Should().ContainSingle()
            .Which.Route.Should().Be(CommunityJoinRoute.Approved);
    }

    [Fact]
    public void Founding_and_a_repeat_join_raise_no_join()
    {
        var community = Opened(requiresApproval: false);

        var founded = CommunityMembership.Found(
            CommunityMembershipId.New(), community, UserId.New(), DateTimeOffset.UtcNow).Value;

        founded.DomainEvents.OfType<CommunityMemberJoinedDomainEvent>().Should().BeEmpty();

        var member = Following(community, CommunityMembershipVisibility.Public);
        member.Join(community, false, DateTimeOffset.UtcNow);
        member.ClearDomainEvents();

        member.Join(community, false, DateTimeOffset.UtcNow).IsFailure.Should().BeTrue();
        member.DomainEvents.OfType<CommunityMemberJoinedDomainEvent>().Should().BeEmpty();
    }

    // ── location.countryChanged: a residence starting ────────────────────────────────────────────

    [Fact]
    public void Declaring_a_current_residence_raises_an_arrival_and_a_past_one_does_not()
    {
        var current = Residence(new DateOnly(2024, 3, 1), null);

        current.DomainEvents.OfType<CountryResidenceStartedDomainEvent>()
            .Should().ContainSingle()
            .Which.CountryCode.Should().Be("GB");

        var past = Residence(new DateOnly(2015, 1, 1), new DateOnly(2019, 1, 1));

        past.DomainEvents.OfType<CountryResidenceStartedDomainEvent>()
            .Should().BeEmpty("a residence already left is history, not where the member lives");
    }

    [Fact]
    public void Reopening_a_residence_raises_an_arrival_and_re_saving_an_open_one_does_not()
    {
        var experience = Residence(new DateOnly(2015, 1, 1), new DateOnly(2019, 1, 1));
        experience.ClearDomainEvents();

        experience.AmendPeriod(Period(new DateOnly(2015, 1, 1), null));

        experience.DomainEvents.OfType<CountryResidenceStartedDomainEvent>()
            .Should().ContainSingle("the member came back");

        experience.ClearDomainEvents();
        experience.AmendPeriod(Period(new DateOnly(2015, 2, 1), null));

        experience.DomainEvents.OfType<CountryResidenceStartedDomainEvent>()
            .Should().BeEmpty("an edit to the current residence is not a move");
    }

    // ── location.enteredArea: what a watch concerns ──────────────────────────────────────────────

    [Fact]
    public void An_area_watch_concerns_only_the_areas_it_names_unless_it_watches_any()
    {
        var watched = Guid.NewGuid();
        var other = Guid.NewGuid();

        new AutomationAreaWatch(false, new HashSet<Guid> { watched })
            .Concerns([other, watched]).Should().BeTrue();

        new AutomationAreaWatch(false, new HashSet<Guid> { watched })
            .Concerns([other]).Should().BeFalse();

        new AutomationAreaWatch(true, new HashSet<Guid>()).Concerns([other]).Should().BeTrue();

        AutomationAreaWatch.Nothing.Concerns([other, watched]).Should().BeFalse();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    private static CommunityAggregate Opened(bool requiresApproval)
    {
        var community = CommunityAggregate.Propose(
            CommunityId.New(),
            CommunitySlug.Create("wiring-" + Guid.NewGuid().ToString("N")[..8]).Value,
            "Wiring test community",
            CommunityType.NationalDiaspora,
            CommunityVisibility.Public,
            "NG").Value;

        community.Open();
        community.SetJoinPolicy(requiresApproval);
        return community;
    }

    private static CommunityMembership Following(
        CommunityAggregate community, CommunityMembershipVisibility visibility)
    {
        var membership = CommunityMembership.Start(
            CommunityMembershipId.New(), community.Id, UserId.New(),
            CommunityMembershipState.Following).Value;

        membership.SetVisibility(visibility);
        return membership;
    }

    private static ResidencePeriod Period(DateOnly arrived, DateOnly? departed) =>
        ResidencePeriod.TryCreate(arrived, departed, new DateOnly(2026, 10, 21)).Value;

    private static CountryExperience Residence(DateOnly arrived, DateOnly? departed) =>
        CountryExperience.Declare(
            UserId.New(), CountryCode.Create("GB"), Period(arrived, departed), "Diaspora Member").Value;
}
