using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.Suggestions;
using FluentAssertions;
using Xunit;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.UnitTests.Guides;

/// <summary>
/// The platform's lightweight merge request, and the line it must not cross.
/// <para>
/// <b>Accepting a suggestion does not edit the guide.</b> The decision is the workflow; the
/// author applies the text through the ordinary editor, where it gets the same validation,
/// revision bump and version history as any other edit. Auto-applying community prose to a
/// published guide would be a path onto readers' screens that bypasses the person whose name is
/// on the guide — which is the one thing this aggregate exists to prevent.
/// </para>
/// <para>
/// The other rule is that a decision is final. A suggestion that could be accepted, rejected
/// and accepted again would make the notification the suggester received a lie, and would make
/// the moderation trail unreadable.
/// </para>
/// </summary>
public class GuideSuggestionTests
{
    private static readonly DateTimeOffset Now = GuideLifecycleTests.Now;

    private static readonly GuideId Subject = GuideId.From(Guid.Parse("00000000-0000-0000-0000-0000000000e1"));

    // ── Submission ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_submitted_suggestion_is_open_and_undecided()
    {
        var suggestion = Submit();

        suggestion.Status.Should().Be(GuideSuggestionStatus.Open);
        suggestion.DecidedBy.Should().BeNull();
        suggestion.DecidedAt.Should().BeNull();
        suggestion.DecisionNote.Should().BeNull();
        suggestion.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Submitting_announces_the_suggestion_so_the_author_hears_about_it()
    {
        var suggestion = Submit();

        var submitted = suggestion.DomainEvents.OfType<GuideSuggestionSubmittedDomainEvent>().Single();

        submitted.GuideId.Should().Be(Subject);
        submitted.AuthorId.Should().Be(GuideLifecycleTests.Reader);
    }

    [Fact]
    public void A_suggestion_may_target_one_section_or_the_whole_guide()
    {
        var sectionId = Guid.NewGuid();

        Submit(targetSectionId: sectionId).TargetSectionId.Should().Be(sectionId);
        Submit().TargetSectionId.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public void A_suggestion_with_nothing_proposed_is_refused(string? content)
    {
        var suggestion = GuideSuggestion.Submit(
            GuideSuggestionId.New(), Subject, GuideLifecycleTests.Reader, content, Now);

        suggestion.IsFailure.Should().BeTrue();
        suggestion.Error.Should().Be(GuideErrors.BlockInvalid);
    }

    [Fact]
    public void A_proposal_past_the_block_ceiling_is_refused()
    {
        var suggestion = GuideSuggestion.Submit(
            GuideSuggestionId.New(),
            Subject,
            GuideLifecycleTests.Reader,
            new string('c', GuideLimits.MaxSuggestionContent + 1),
            Now);

        suggestion.IsFailure.Should().BeTrue();
        suggestion.Error.Should().Be(GuideErrors.BlockInvalid);
    }

    [Fact]
    public void A_rationale_is_truncated_rather_than_costing_the_suggestion()
    {
        // Supporting prose. Losing a tail nobody will read is better than losing the suggestion.
        var suggestion = Submit(rationale: new string('r', GuideLimits.MaxSuggestionRationale + 50));

        suggestion.Rationale!.Length.Should().Be(GuideLimits.MaxSuggestionRationale);
    }

    // ── Deciding ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Accepting_records_who_decided_and_when()
    {
        var suggestion = Submit();
        suggestion.ClearDomainEvents();

        var accepted = suggestion.Accept(
            GuideLifecycleTests.Author, Now.AddDays(1), "Good catch, applying it.");

        accepted.IsSuccess.Should().BeTrue();
        suggestion.Status.Should().Be(GuideSuggestionStatus.Accepted);
        suggestion.DecidedBy.Should().Be(GuideLifecycleTests.Author);
        suggestion.DecidedAt.Should().Be(Now.AddDays(1));
        suggestion.DecisionNote.Should().Be("Good catch, applying it.");

        var decided = suggestion.DomainEvents.OfType<GuideSuggestionDecidedDomainEvent>().Single();
        decided.Status.Should().Be(GuideSuggestionStatus.Accepted);
        decided.DecidedBy.Should().Be(GuideLifecycleTests.Author);
    }

    [Fact]
    public void Rejecting_records_the_explanation_the_suggester_is_shown()
    {
        var suggestion = Submit();

        suggestion.Reject(GuideLifecycleTests.Author, Now.AddDays(1), "The rule changed last month.")
            .IsSuccess.Should().BeTrue();

        suggestion.Status.Should().Be(GuideSuggestionStatus.Rejected);
        suggestion.DecisionNote.Should().Be("The rule changed last month.");
    }

    [Fact]
    public void A_decision_note_is_optional()
    {
        var suggestion = Submit();

        suggestion.Reject(GuideLifecycleTests.Author, Now.AddDays(1)).IsSuccess.Should().BeTrue();

        suggestion.DecisionNote.Should().BeNull();
    }

    [Fact]
    public void A_decided_suggestion_cannot_be_decided_again()
    {
        var suggestion = Submit();

        suggestion.Accept(GuideLifecycleTests.Author, Now.AddDays(1)).IsSuccess.Should().BeTrue();

        var reversed = suggestion.Reject(GuideLifecycleTests.Author, Now.AddDays(2), "Changed my mind.");

        reversed.IsFailure.Should().BeTrue();
        reversed.Error.Should().Be(GuideErrors.SuggestionClosed,
            "the suggester was already told what happened, and the trail has to stay readable");

        suggestion.Status.Should().Be(GuideSuggestionStatus.Accepted);
    }

    [Fact]
    public void Accepting_twice_is_refused_rather_than_quietly_succeeding()
    {
        var suggestion = Submit();

        suggestion.Accept(GuideLifecycleTests.Author, Now.AddDays(1)).IsSuccess.Should().BeTrue();
        suggestion.ClearDomainEvents();

        suggestion.Accept(GuideLifecycleTests.Author, Now.AddDays(2))
            .Error.Should().Be(GuideErrors.SuggestionClosed);

        suggestion.DomainEvents.Should().BeEmpty(
            "a second notification about the same decision is a bug the suggester can see");
    }

    // ── Withdrawal ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Only_the_suggester_may_withdraw_their_own_suggestion()
    {
        var suggestion = Submit();

        var byAuthor = suggestion.Withdraw(GuideLifecycleTests.Author, Now.AddDays(1));

        byAuthor.IsFailure.Should().BeTrue();
        byAuthor.Error.Should().Be(GuideErrors.SuggestionNotAuthor,
            "a guide's author declining a suggestion is a rejection, which the suggester is told about");

        suggestion.Status.Should().Be(GuideSuggestionStatus.Open);
    }

    [Fact]
    public void The_suggester_may_take_their_suggestion_back()
    {
        var suggestion = Submit();
        suggestion.ClearDomainEvents();

        suggestion.Withdraw(GuideLifecycleTests.Reader, Now.AddDays(1)).IsSuccess.Should().BeTrue();

        suggestion.Status.Should().Be(GuideSuggestionStatus.Withdrawn);
        suggestion.DecidedBy.Should().Be(GuideLifecycleTests.Reader);
        suggestion.DecidedAt.Should().Be(Now.AddDays(1));

        suggestion.DomainEvents.OfType<GuideSuggestionDecidedDomainEvent>().Single()
            .Status.Should().Be(GuideSuggestionStatus.Withdrawn);
    }

    [Fact]
    public void A_decided_suggestion_cannot_be_withdrawn()
    {
        var suggestion = Submit();

        suggestion.Accept(GuideLifecycleTests.Author, Now.AddDays(1)).IsSuccess.Should().BeTrue();

        suggestion.Withdraw(GuideLifecycleTests.Reader, Now.AddDays(2))
            .Error.Should().Be(GuideErrors.SuggestionClosed);
    }

    [Fact]
    public void A_withdrawn_suggestion_cannot_be_accepted_afterwards()
    {
        var suggestion = Submit();

        suggestion.Withdraw(GuideLifecycleTests.Reader, Now.AddDays(1)).IsSuccess.Should().BeTrue();

        suggestion.Accept(GuideLifecycleTests.Author, Now.AddDays(2))
            .Error.Should().Be(GuideErrors.SuggestionClosed);
    }

    // ── The line the workflow must not cross ────────────────────────────────────────────────

    [Fact]
    public void Accepting_a_suggestion_leaves_the_guide_completely_untouched()
    {
        var guide = GuideLifecycleTests.Published(
            GuideLifecycleTests.OneSection("Step one", "Book the appointment online."));

        var revision = guide.Revision;
        var version = guide.CurrentVersion;

        guide.ClearDomainEvents();

        var suggestion = Submit(proposedContent: "Walk in without an appointment instead.");

        suggestion.Accept(GuideLifecycleTests.Author, Now.AddDays(1)).IsSuccess.Should().BeTrue();

        guide.Sections[0].Blocks[0].Content.Should().Be("Book the appointment online.",
            "community text must never reach a reader's screen without passing the author's editor");

        guide.Revision.Should().Be(revision);
        guide.CurrentVersion.Should().Be(version);
        guide.DomainEvents.Should().BeEmpty();
    }

    private static GuideSuggestion Submit(
        string? proposedContent = "The branch also accepts a tenancy agreement.",
        Guid? targetSectionId = null,
        string? rationale = null) =>
        GuideSuggestion.Submit(
            GuideSuggestionId.New(),
            Subject,
            GuideLifecycleTests.Reader,
            proposedContent,
            Now,
            targetSectionId,
            rationale).Value;
}
