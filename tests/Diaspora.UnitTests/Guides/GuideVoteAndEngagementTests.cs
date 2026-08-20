using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Votes;
using FluentAssertions;
using Xunit;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.UnitTests.Guides;

/// <summary>
/// Helpfulness votes, and the rollup a discovery card renders.
/// <para>
/// A vote is an aggregate of its own so two members voting at once do not contend on the guide
/// row, which means "one vote per member per guide" has to be enforced by a database index —
/// and an EF index cannot span an owned type's columns and the aggregate's own, which is B6's
/// recorded lesson. <see cref="GuideVote.VoterKey"/> is the denormalised key that makes the
/// index expressible; it must be derived and deterministic or the index guards nothing.
/// </para>
/// <para>
/// The rollup on the guide is a copy, applied as a delta by the handler. It is clamped at zero
/// rather than trusted, because a decrement that arrives twice is the ordinary consequence of
/// at-least-once delivery.
/// </para>
/// </summary>
public class GuideVoteAndEngagementTests
{
    private static readonly DateTimeOffset Now = GuideLifecycleTests.Now;

    private static readonly GuideId Guide = GuideId.From(Guid.Parse("00000000-0000-0000-0000-0000000000d1"));

    // ── Casting ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(GuideVoteKind.Helpful)]
    [InlineData(GuideVoteKind.NotHelpful)]
    public void A_vote_records_the_kind_the_member_chose(GuideVoteKind kind)
    {
        var vote = GuideVote.Cast(GuideVoteId.New(), Guide, GuideLifecycleTests.Reader, kind, Now);

        vote.IsSuccess.Should().BeTrue();
        vote.Value.Kind.Should().Be(kind);
        vote.Value.GuideId.Should().Be(Guide);
        vote.Value.VoterId.Should().Be(GuideLifecycleTests.Reader);
        vote.Value.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Casting_announces_a_first_vote_with_no_previous_kind()
    {
        var vote = Cast(GuideVoteKind.Helpful);

        var cast = vote.DomainEvents.OfType<GuideVoteCastDomainEvent>().Single();

        cast.Kind.Should().Be(GuideVoteKind.Helpful);
        cast.PreviousKind.Should().BeNull(
            "the handler computes the rollup delta from the previous kind, and a first vote has none");
    }

    // ── Changing a vote ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Changing_a_vote_announces_what_it_used_to_be()
    {
        var vote = Cast(GuideVoteKind.Helpful);
        vote.ClearDomainEvents();

        var changed = vote.ChangeKind(GuideVoteKind.NotHelpful);

        changed.IsSuccess.Should().BeTrue();
        vote.Kind.Should().Be(GuideVoteKind.NotHelpful);

        var cast = vote.DomainEvents.OfType<GuideVoteCastDomainEvent>().Single();
        cast.Kind.Should().Be(GuideVoteKind.NotHelpful);
        cast.PreviousKind.Should().Be(GuideVoteKind.Helpful,
            "without it the handler would add one helpful and never remove the other");
    }

    [Fact]
    public void Recasting_the_same_vote_is_refused_so_no_rollup_is_recomputed_for_a_no_op()
    {
        var vote = Cast(GuideVoteKind.Helpful);
        vote.ClearDomainEvents();

        var unchanged = vote.ChangeKind(GuideVoteKind.Helpful);

        unchanged.IsFailure.Should().BeTrue();
        unchanged.Error.Should().Be(GuideErrors.VoteUnchanged);
        vote.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void A_member_may_change_their_mind_repeatedly()
    {
        var vote = Cast(GuideVoteKind.Helpful);

        vote.ChangeKind(GuideVoteKind.NotHelpful).IsSuccess.Should().BeTrue();
        vote.ChangeKind(GuideVoteKind.Helpful).IsSuccess.Should().BeTrue();

        vote.Kind.Should().Be(GuideVoteKind.Helpful);
        vote.DomainEvents.OfType<GuideVoteCastDomainEvent>().Should().HaveCount(3);
    }

    // ── The denormalised key ────────────────────────────────────────────────────────────────

    [Fact]
    public void The_voter_key_is_derived_from_the_pair_the_index_has_to_guard()
    {
        var vote = Cast(GuideVoteKind.Helpful);

        vote.VoterKey.Should().Be(
            $"{Guide.Value:N}:{GuideLifecycleTests.Reader.Value:N}",
            "the database enforces one vote per member per guide with this column and nothing else");
    }

    [Fact]
    public void The_voter_key_is_deterministic_so_two_rows_for_one_member_collide()
    {
        var first = GuideVote.Cast(
            GuideVoteId.New(), Guide, GuideLifecycleTests.Reader, GuideVoteKind.Helpful, Now).Value;

        var second = GuideVote.Cast(
            GuideVoteId.New(), Guide, GuideLifecycleTests.Reader, GuideVoteKind.NotHelpful,
            Now.AddHours(1)).Value;

        second.VoterKey.Should().Be(first.VoterKey,
            "a second row for the same pair must violate the unique index, not sit beside the first");
    }

    [Fact]
    public void Different_members_and_different_guides_get_different_keys()
    {
        var mine = Cast(GuideVoteKind.Helpful).VoterKey;

        var anotherMember = GuideVote.Cast(
            GuideVoteId.New(), Guide, GuideLifecycleTests.Editor, GuideVoteKind.Helpful, Now)
            .Value.VoterKey;

        var anotherGuide = GuideVote.Cast(
            GuideVoteId.New(), GuideId.New(), GuideLifecycleTests.Reader, GuideVoteKind.Helpful, Now)
            .Value.VoterKey;

        mine.Should().NotBe(anotherMember);
        mine.Should().NotBe(anotherGuide);
    }

    [Fact]
    public void Changing_a_vote_does_not_change_its_key()
    {
        var vote = Cast(GuideVoteKind.Helpful);
        var key = vote.VoterKey;

        vote.ChangeKind(GuideVoteKind.NotHelpful).IsSuccess.Should().BeTrue();

        vote.VoterKey.Should().Be(key, "changing your mind is not becoming a different voter");
    }

    // ── The rollup on the guide ─────────────────────────────────────────────────────────────

    [Fact]
    public void A_helpful_vote_moves_only_the_helpful_count()
    {
        var guide = GuideLifecycleTests.Published();

        guide.ApplyVoteDelta(helpfulDelta: 1, notHelpfulDelta: 0);

        guide.Engagement.HelpfulCount.Should().Be(1);
        guide.Engagement.NotHelpfulCount.Should().Be(0);
    }

    [Fact]
    public void Changing_a_vote_moves_one_count_down_as_the_other_goes_up()
    {
        var guide = GuideLifecycleTests.Published();

        guide.ApplyVoteDelta(1, 0);

        // What the handler applies when the event carries a previous kind.
        guide.ApplyVoteDelta(-1, 1);

        guide.Engagement.HelpfulCount.Should().Be(0);
        guide.Engagement.NotHelpfulCount.Should().Be(1);
    }

    [Fact]
    public void A_redelivered_retraction_cannot_take_a_count_below_zero()
    {
        var guide = GuideLifecycleTests.Published();

        guide.ApplyVoteDelta(1, 0);
        guide.ApplyVoteDelta(-1, 0);
        guide.ApplyVoteDelta(-1, 0);

        guide.Engagement.HelpfulCount.Should().Be(0,
            "a card showing '-1 helpful' is worse than one that is briefly one off");
    }

    [Fact]
    public void Shares_and_bookmarks_accumulate_independently_of_votes()
    {
        var guide = GuideLifecycleTests.Published();

        guide.RecordShare();
        guide.RecordShare();
        guide.ApplyBookmarkDelta(4);
        guide.ApplyBookmarkDelta(-1);
        guide.ApplyVoteDelta(2, 0);

        guide.Engagement.ShareCount.Should().Be(2);
        guide.Engagement.BookmarkCount.Should().Be(3);
        guide.Engagement.HelpfulCount.Should().Be(2);
    }

    private static GuideVote Cast(GuideVoteKind kind) =>
        GuideVote.Cast(GuideVoteId.New(), Guide, GuideLifecycleTests.Reader, kind, Now).Value;
}
