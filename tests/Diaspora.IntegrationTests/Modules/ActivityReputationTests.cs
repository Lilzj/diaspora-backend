using System.Runtime.CompilerServices;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Contracts.CommunityProgramme.V1.Events;
using Diaspora.Contracts.Events;
using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Contracts.Marketplace.V1.Events;
using Diaspora.Contracts.Providers.V1.Events;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Trust.Application.Consumers;
using Diaspora.Modules.Trust.Domain;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// XP for what the rest of the platform publishes reaches the member's breakdown (REP-14, D-3).
/// <para>
/// The unit tests price each activity and prove who is paid. This proves the half they cannot: that
/// each consumer is one the module registers for its event, that the award is saved, and that the
/// member reads it on <c>GET /trust/reputation/me</c> under its own label.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class ActivityReputationTests : PooledIdentityTestBase
{
    public ActivityReputationTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Each_activity_reads_on_the_breakdown_under_its_own_label()
    {
        var member = await MemberAsync();

        await DeliverAsync<EventPublishedReputationConsumer, EventPublishedIntegrationEvent>(
            Blank<EventPublishedIntegrationEvent>() with
            {
                CommunityEventId = Guid.NewGuid(), OrganizerUserId = member, CountryCode = "NG"
            });
        await DeliverAsync<ProviderPublishedReputationConsumer, ProviderPublishedIntegrationEvent>(
            Blank<ProviderPublishedIntegrationEvent>() with
            {
                ProviderId = Guid.NewGuid(), OwnerId = member, CountryCode = "NG"
            });
        await DeliverAsync<JobPublishedReputationConsumer, JobPublishedIntegrationEvent>(
            Blank<JobPublishedIntegrationEvent>() with
            {
                JobId = Guid.NewGuid(), EmployerUserId = member, CountryCode = "NG"
            });
        await DeliverAsync<HousingPublishedReputationConsumer, HousingPublishedIntegrationEvent>(
            Blank<HousingPublishedIntegrationEvent>() with
            {
                ListingId = Guid.NewGuid(), LandlordUserId = member, CountryCode = "NG"
            });
        await DeliverAsync<ListingPublishedReputationConsumer, ListingPublishedIntegrationEvent>(
            Blank<ListingPublishedIntegrationEvent>() with
            {
                ListingId = Guid.NewGuid(), SellerUserId = member, CountryCode = "NG"
            });
        await DeliverAsync<NeighborhoodInsightApprovedReputationConsumer, NeighborhoodInsightApprovedIntegrationEvent>(
            Blank<NeighborhoodInsightApprovedIntegrationEvent>() with
            {
                InsightId = Guid.NewGuid(), AuthorId = member
            });
        await DeliverAsync<VolunteerHoursConfirmedReputationConsumer, VolunteerHoursConfirmedIntegrationEvent>(
            Blank<VolunteerHoursConfirmedIntegrationEvent>() with
            {
                AssignmentId = Guid.NewGuid(), VolunteerUserId = member
            });
        await DeliverAsync<ProviderRecommendationReputationConsumer, ProviderRecommendationReceivedIntegrationEvent>(
            Blank<ProviderRecommendationReceivedIntegrationEvent>() with
            {
                RecommendationId = Guid.NewGuid(), ProviderId = Guid.NewGuid(),
                RecommenderId = Guid.NewGuid(), OwnerUserId = member
            });

        var reputation = await ReputationAsync();
        var contribution = Dimension(reputation, "contribution");

        contribution.Lines.Should().Contain(
        [
            new LineView(ReputationActions.EventPublished, "Events published", 1, 10),
            new LineView(ReputationActions.ProviderPublished, "Services published", 1, 10),
            new LineView(ReputationActions.JobPublished, "Jobs posted", 1, 5),
            new LineView(ReputationActions.HousingPublished, "Homes listed", 1, 5),
            new LineView(ReputationActions.ListingPublished, "Items listed for sale", 1, 5),
            new LineView(ReputationActions.NeighborhoodInsightApproved, "Neighbourhood insights approved", 1, 15),
            new LineView(ReputationActions.VolunteerHoursConfirmed, "Volunteering confirmed", 1, 15)
        ]);
        Dimension(reputation, "trust").Lines.Should().Contain(
            new LineView(ReputationActions.ProviderRecommendationReceived, "Recommendations received", 1, 2));
    }

    [Fact]
    public async Task A_cancelled_event_takes_back_what_publishing_it_earned()
    {
        var member = await MemberAsync();
        var before = (await ReputationAsync()).ContributionScore;
        var eventId = Guid.NewGuid();

        await DeliverAsync<EventPublishedReputationConsumer, EventPublishedIntegrationEvent>(
            Blank<EventPublishedIntegrationEvent>() with
            {
                CommunityEventId = eventId, OrganizerUserId = member, CountryCode = "NG"
            });

        (await ReputationAsync()).ContributionScore.Should().Be(before + 10);

        await DeliverAsync<EventCancelledReversalConsumer, EventCancelledIntegrationEvent>(
            Blank<EventCancelledIntegrationEvent>() with { CommunityEventId = eventId });

        var reputation = await ReputationAsync();
        reputation.ContributionScore.Should().Be(before);
        Dimension(reputation, "contribution").Lines
            .Should().NotContain(line => line.ActionType == ReputationActions.EventPublished,
                "a published and cancelled event nets to nothing");
    }

    /// <summary>
    /// A contract with only the fields Trust reads set; the rest of its thirty or forty required
    /// members are left unset, as <c>AttributeEnrichmentTests</c> builds its contracts.
    /// </summary>
    private static T Blank<T>()
        where T : IIntegrationEvent =>
        (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    private async Task<Guid> MemberAsync()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        return auth.User.Id;
    }

    /// <summary>
    /// Delivers through the handler the host registered for the event, not one built for the test,
    /// so an unregistered consumer fails here rather than going quiet in production.
    /// </summary>
    private async Task DeliverAsync<TConsumer, TEvent>(TEvent integrationEvent)
        where TConsumer : IntegrationEventHandler<TEvent>
        where TEvent : IIntegrationEvent
    {
        using var scope = Factory.Services.CreateScope();

        var consumer = scope.ServiceProvider
            .GetServices<INotificationHandler<IntegrationEventNotification<TEvent>>>()
            .OfType<TConsumer>()
            .Should().ContainSingle("the Trust module registers {0}", typeof(TConsumer).Name).Subject;

        await consumer.Handle(new IntegrationEventNotification<TEvent>(integrationEvent), CancellationToken.None);
    }

    private async Task<ReputationView> ReputationAsync() =>
        await IdentityApiClient.ReadDataAsync<ReputationView>(
            await HttpClient.GetAsync("/api/v1/trust/reputation/me"));

    private static DimensionView Dimension(ReputationView reputation, string name) =>
        reputation.Breakdown.Single(dimension => dimension.Dimension == name);

    private sealed record ReputationView(
        int ContributionScore, int VerificationScore, int TrustScore, List<DimensionView> Breakdown);

    private sealed record DimensionView(string Dimension, int Score, List<LineView> Lines, int Adjustment);

    private sealed record LineView(string ActionType, string Label, int Count, int Points);
}
