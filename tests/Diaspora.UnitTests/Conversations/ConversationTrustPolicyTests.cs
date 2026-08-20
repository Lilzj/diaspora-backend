using Diaspora.Modules.Conversations.Extensibility;
using Diaspora.Modules.Conversations.Extensibility.Policies;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Services.Models;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Conversations;

/// <summary>
/// The two policies that join a platform suspension to the messaging surface.
/// <para>
/// B13.2 recorded the gap as D-3 and classified it a deployment's job: <em>"Trust's account-level
/// bans do not currently prevent opening a conversation; per-conversation blocking does."</em>
/// These assert the four behaviours that make closing it safe — refuse a suspension, refuse a
/// restriction on the initiating side only, allow the two states that are not restrictions, and say
/// nothing about the other member.
/// </para>
/// </summary>
public sealed class ConversationTrustPolicyTests
{
    private static readonly UserId Initiator =
        UserId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    private static readonly UserId Recipient =
        UserId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    // ── The initiator ────────────────────────────────────────────────────

    [Fact]
    public async Task A_suspended_member_cannot_open_a_conversation()
    {
        var decision = await InitiatorAsync(AccountStanding.Suspended);

        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Contain("suspended");
    }

    [Fact]
    public async Task A_restricted_member_cannot_open_a_conversation_and_is_told_they_may_still_reply()
    {
        var decision = await InitiatorAsync(AccountStanding.Restricted);

        decision.Allowed.Should().BeFalse();

        // The wording carries the asymmetry the design depends on: this policy is consulted when a
        // thread is opened and never when one is answered, so a restricted member is not cut off
        // from a landlord or an employer who is already talking to them.
        decision.Reason.Should().Contain("reply");
    }

    /// <summary>
    /// The one case an ordinal comparison would get wrong. <c>UnderReview</c> sorts above
    /// <c>Restricted</c> in the enum and is not a restriction — a member waiting on the platform's
    /// own queue has had nothing taken from them.
    /// </summary>
    [Fact]
    public async Task A_member_under_review_may_still_open_a_conversation()
    {
        (await InitiatorAsync(AccountStanding.UnderReview)).Allowed.Should().BeTrue();
    }

    /// <summary>
    /// Everybody between registering and being provisioned a trust profile answers
    /// <c>Unknown</c>, and so does every member on a deployment where Trust is not composed.
    /// Refusing that would refuse the platform.
    /// </summary>
    [Theory]
    [InlineData(AccountStanding.Unknown)]
    [InlineData(AccountStanding.Good)]
    public async Task An_unencumbered_member_may_open_a_conversation(AccountStanding standing)
    {
        (await InitiatorAsync(standing)).Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task The_initiator_policy_asks_about_the_initiator_and_nobody_else()
    {
        var trust = new RecordingTrustService(AccountStanding.Good);

        await new InitiatorAccountStandingPolicy(trust).EvaluateAsync(Context());

        trust.Asked.Should().ContainSingle().Which.Should().Be(Initiator);
    }

    // ── The recipient ────────────────────────────────────────────────────

    [Fact]
    public async Task Nobody_starts_a_conversation_with_a_suspended_account()
    {
        var decision = await RecipientAsync(AccountStanding.Suspended);

        decision.Allowed.Should().BeFalse();
    }

    /// <summary>
    /// <b>The privacy property, and the reason this policy is separate from the other one.</b>
    /// Another member's moderation state is theirs. A refusal that named it would turn opening a
    /// conversation into a probe for who the platform has acted against, and the handler maps every
    /// policy refusal onto the same error code a block produces so the two cannot be told apart.
    /// </summary>
    [Fact]
    public async Task The_recipient_refusal_says_nothing_about_the_recipient()
    {
        var decision = await RecipientAsync(AccountStanding.Suspended);

        decision.Reason.Should().NotBeNull();

        decision.Reason!.ToLowerInvariant().Should().NotContain("suspend")
            .And.NotContain("restrict")
            .And.NotContain("ban")
            .And.NotContain("review");
    }

    /// <summary>
    /// A restricted member may read and react — they can answer. Only a suspension makes the
    /// thread useless, so only a suspension is refused on this side.
    /// </summary>
    [Theory]
    [InlineData(AccountStanding.Restricted)]
    [InlineData(AccountStanding.UnderReview)]
    [InlineData(AccountStanding.Unknown)]
    [InlineData(AccountStanding.Good)]
    public async Task Every_other_recipient_standing_is_permitted(AccountStanding standing)
    {
        (await RecipientAsync(standing)).Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task The_recipient_policy_asks_about_the_recipient_and_nobody_else()
    {
        var trust = new RecordingTrustService(AccountStanding.Good);

        await new RecipientAccountStandingPolicy(trust).EvaluateAsync(Context());

        trust.Asked.Should().ContainSingle().Which.Should().Be(Recipient);
    }

    // ── Both ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The seam's contract is that a policy may only refuse. Nothing here returns a decision that
    /// could re-permit a conversation the handler already refused, because the handler's loop reads
    /// <c>Allowed</c> and never re-checks anything on the strength of it.
    /// </summary>
    [Fact]
    public void Both_policies_carry_a_stable_namespaced_name()
    {
        var trust = new RecordingTrustService(AccountStanding.Good);

        new InitiatorAccountStandingPolicy(trust).Name
            .Should().Be("conversations.initiator-account-standing");

        new RecipientAccountStandingPolicy(trust).Name
            .Should().Be("conversations.recipient-account-standing");
    }

    private static Task<ConversationPolicyDecision> InitiatorAsync(AccountStanding standing) =>
        new InitiatorAccountStandingPolicy(new RecordingTrustService(standing)).EvaluateAsync(Context());

    private static Task<ConversationPolicyDecision> RecipientAsync(AccountStanding standing) =>
        new RecipientAccountStandingPolicy(new RecordingTrustService(standing)).EvaluateAsync(Context());

    private static ConversationPolicyContext Context() => new()
    {
        InitiatorUserId = Initiator,
        RecipientUserId = Recipient,
        ContextTypeKey = "MarketplaceListing",
        EntityId = "listing-1"
    };

    /// <summary>
    /// Answers one standing and remembers who was asked, so the tests can assert the second
    /// property that matters: each policy reads exactly one side of the pair.
    /// </summary>
    private sealed class RecordingTrustService : ITrustService
    {
        private readonly AccountStanding _standing;

        public RecordingTrustService(AccountStanding standing) => _standing = standing;

        public List<UserId> Asked { get; } = [];

        public Task<AccountStanding> GetAccountStandingAsync(
            UserId userId, CancellationToken cancellationToken = default)
        {
            Asked.Add(userId);
            return Task.FromResult(_standing);
        }

        public Task<TrustLevel> GetTrustLevelAsync(
            UserId userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(TrustLevel.Basic);

        public Task<bool> IsPublishingEligibleAsync(
            UserId userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
