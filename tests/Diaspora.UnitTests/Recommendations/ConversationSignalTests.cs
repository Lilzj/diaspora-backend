using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Conversations.V1.Events;
using Diaspora.Modules.Recommendations.Application.Consumers.Conversations;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Recommendations;

/// <summary>
/// The conversation join PSR-1.2 built.
/// <para>
/// B13.2's event catalogue has a <c>Consumed by</c> column naming this module against four of its
/// contracts. Nothing was built — the same shape PSR-1.1 found between the Financial Platform and
/// its two universal consumers, and for the same structural reason: a phase can only build its own
/// half.
/// </para>
/// </summary>
public sealed class ConversationSignalTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid Initiator = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Seller = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Listing = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // ── The map ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("MarketplaceListing", RecommendationTargetType.MarketplaceListing)]
    [InlineData("HousingListing", RecommendationTargetType.HousingListing)]
    [InlineData("JobOpportunity", RecommendationTargetType.Job)]
    [InlineData("ProviderInquiry", RecommendationTargetType.Provider)]
    [InlineData("CommunityEvent", RecommendationTargetType.Event)]
    [InlineData("Organization", RecommendationTargetType.Organization)]
    [InlineData("CommunityGuide", RecommendationTargetType.Guide)]
    public void Every_recommendable_context_translates(
        string contextType, RecommendationTargetType expected) =>
        ConversationContextMap.Translate(contextType).Should().Be(expected);

    /// <summary>
    /// <b>The five unmapped context types, and why each is a decision.</b> A private conversation and
    /// a business account are about a person, and this module has no member target. The three
    /// system-managed types are worse than unmappable: treating a member's appeal against a
    /// moderation decision as an expression of interest would be grotesque as well as useless.
    /// </summary>
    [Theory]
    [InlineData("PrivateConversation")]
    [InlineData("BusinessAccount")]
    [InlineData("SupportTicket")]
    [InlineData("TrustAppeal")]
    [InlineData("AiAssistant")]
    [InlineData("SomethingAVerticalContributesLater")]
    [InlineData("")]
    [InlineData(null)]
    public void A_context_with_nothing_to_recommend_translates_to_nothing(string? contextType) =>
        ConversationContextMap.Translate(contextType).Should().BeNull();

    [Fact]
    public void The_context_vocabulary_survives_the_wire() =>
        ConversationContextMap.Translate("  marketplacelisting  ")
            .Should().Be(RecommendationTargetType.MarketplaceListing);

    // ── The consumer ─────────────────────────────────────────────────────

    [Fact]
    public async Task Opening_a_conversation_about_a_listing_records_an_intent_signal()
    {
        var recorded = await ConsumeAsync(Created());

        recorded.Should().ContainSingle();

        var signal = recorded[0];

        signal.UserId.Should().Be(UserId.From(Initiator));
        signal.Kind.Should().Be(SignalKind.Applied);
        signal.Source.Should().Be(SignalSource.Conversations);
        signal.TargetType.Should().Be(RecommendationTargetType.MarketplaceListing);
        signal.TargetId.Should().Be(Listing);
        signal.OccurredAt.Should().Be(Now);
    }

    /// <summary>
    /// <b>Only one participant chose to be there.</b> Crediting the seller with an affinity for their
    /// own listing would teach the platform to recommend people the things they are selling, and the
    /// error compounds with how busy they are.
    /// </summary>
    [Fact]
    public async Task Only_the_initiator_is_credited_with_the_interest()
    {
        var recorded = await ConsumeAsync(Created());

        recorded.Select(signal => signal.UserId)
            .Should().NotContain(UserId.From(Seller));
    }

    /// <summary>
    /// The provenance is what makes "why was I shown this" answerable, and PSR-1.1 established that
    /// it must be the namespace-qualified contract name — Jobs and Marketplace both publish an
    /// <c>OfferAccepted</c>.
    /// </summary>
    [Fact]
    public async Task The_signal_names_the_contract_it_came_from()
    {
        var recorded = await ConsumeAsync(Created());

        recorded[0].SourceEventName
            .Should().Be(typeof(ConversationCreatedIntegrationEvent).FullName);
    }

    /// <summary>
    /// A support ticket, a trust appeal or a private conversation produces nothing at all. The
    /// consumer's own base class documents that as normal rather than exceptional.
    /// </summary>
    [Fact]
    public async Task A_conversation_about_nothing_recommendable_records_nothing()
    {
        var recorded = await ConsumeAsync(Created(contextType: "PrivateConversation", entityId: Initiator.ToString()));

        recorded.Should().BeEmpty();
    }

    /// <summary>
    /// <b>The entity identifier is opaque on the wire.</b> The owning module decides what it is, and
    /// a context type could carry a slug or a composite key. A signal about a target this module
    /// cannot address is a row that joins to nothing.
    /// </summary>
    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task An_entity_identifier_this_module_cannot_address_records_nothing(string entityId)
    {
        (await ConsumeAsync(Created(entityId: entityId))).Should().BeEmpty();
    }

    // ── Fixtures ─────────────────────────────────────────────────────────

    private static ConversationCreatedIntegrationEvent Created(
        string contextType = "MarketplaceListing", string? entityId = null) => new()
        {
            ConversationId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            ContextType = contextType,
            EntityId = entityId ?? Listing.ToString(),
            InitiatorUserId = Initiator,
            ParticipantUserIds = [Initiator, Seller],
            Status = "Active",
            CreatedAt = Now
        };

    private static async Task<List<SignalDraft>> ConsumeAsync(
        ConversationCreatedIntegrationEvent integrationEvent)
    {
        var recorded = new List<SignalDraft>();

        var signals = Substitute.For<ISignalStore>();
        signals.RecordAsync(Arg.Do<SignalDraft>(recorded.Add), Arg.Any<CancellationToken>())
            .Returns(true);

        var consumer = new ConversationCreatedConsumer(
            new CandidateProjector(
                Substitute.For<IRecommendationCandidateRepository>(),
                Substitute.For<IGuidGenerator>()),
            signals);

        await consumer.Handle(
            new IntegrationEventNotification<ConversationCreatedIntegrationEvent>(integrationEvent),
            CancellationToken.None);

        return recorded;
    }
}
