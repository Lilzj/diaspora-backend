using System.Reflection;
using Diaspora.Contracts.Moderation.V1;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// A moderation decision pauses the placements over what it hid, and only a decision resumes them
/// (7 Nov 2026).
/// <para>
/// <b>The two pauses never mix.</b> A placement the buyer paused stays paused whatever a moderator
/// decides, and a placement a decision paused is not the buyer's to resume while the subject is
/// hidden. Either way the paid days are banked and handed back exactly as a buyer's pause does.
/// </para>
/// </summary>
public class PlacementModerationPauseTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_moderation_pause_banks_the_remaining_days_and_a_moderation_resume_hands_them_back()
    {
        var placement = Purchase(days: 30);
        placement.Activate(Start);
        var originalEnd = placement.Window.EndsAt!.Value;
        var caseId = Guid.NewGuid();

        placement.PauseForModeration(caseId, Start.AddDays(3)).Should().BeTrue();

        placement.Status.Should().Be(PlacementStatus.Paused);
        placement.ModerationCaseId.Should().Be(caseId);
        placement.IsPausedForModeration.Should().BeTrue();

        placement.ResumeAfterModeration(Start.AddDays(8)).Should().BeTrue();

        placement.Status.Should().Be(PlacementStatus.Active);
        placement.ModerationCaseId.Should().BeNull();
        placement.PausedDuration.Should().Be(TimeSpan.FromDays(5));
        placement.Window.EndsAt.Should().Be(originalEnd.AddDays(5));
    }

    [Fact]
    public void A_scheduled_placement_is_paused_too_and_comes_back_scheduled()
    {
        var placement = Purchase(days: 7, startsIn: TimeSpan.FromDays(10));

        placement.PauseForModeration(Guid.NewGuid(), Start).Should().BeTrue();
        placement.ResumeAfterModeration(Start.AddDays(1)).Should().BeTrue();

        placement.Status.Should().Be(PlacementStatus.Scheduled);
    }

    [Fact]
    public void A_buyers_own_pause_is_neither_taken_over_nor_lifted_by_a_moderation_decision()
    {
        var placement = Purchase();
        placement.Activate(Start);
        placement.Pause(Start.AddDays(1));

        placement.PauseForModeration(Guid.NewGuid(), Start.AddDays(2)).Should().BeFalse();
        placement.ModerationCaseId.Should().BeNull();

        placement.ResumeAfterModeration(Start.AddDays(3)).Should().BeFalse();
        placement.Status.Should().Be(PlacementStatus.Paused, "the buyer paused it, and the buyer resumes it");
        placement.PausedAt.Should().Be(Start.AddDays(1));
    }

    [Fact]
    public void The_buyer_cannot_resume_a_placement_a_moderation_decision_paused()
    {
        var placement = Purchase();
        placement.Activate(Start);
        placement.PauseForModeration(Guid.NewGuid(), Start.AddDays(1));

        var resume = () => placement.Resume(Start.AddDays(2));

        resume.Should().Throw<DomainException>()
            .Which.Code.Should().Be(FinancialErrorCodes.PlacementHeldByModeration);
        placement.Status.Should().Be(PlacementStatus.Paused);
    }

    [Fact]
    public void An_ended_placement_is_not_paused_and_a_repeated_decision_changes_nothing()
    {
        var ended = Purchase();
        ended.Activate(Start);
        ended.Cancel("Sold.", Start.AddDays(1));

        ended.PauseForModeration(Guid.NewGuid(), Start.AddDays(2)).Should().BeFalse();
        ended.Status.Should().Be(PlacementStatus.Cancelled);

        var placement = Purchase();
        placement.Activate(Start);
        var caseId = Guid.NewGuid();

        placement.PauseForModeration(caseId, Start.AddDays(1)).Should().BeTrue();
        placement.PauseForModeration(caseId, Start.AddDays(2)).Should().BeFalse("a removal after its hide");
        placement.PausedAt.Should().Be(Start.AddDays(1));

        placement.ResumeAfterModeration(Start.AddDays(3)).Should().BeTrue();
        placement.ResumeAfterModeration(Start.AddDays(4)).Should().BeFalse("a redelivered restore");
    }

    // ── What a placement calls a case's content ─────────────────────────────────────────────

    public static TheoryData<string, MonetizationSubjectType?> ContentTypes => new()
    {
        { ModerationContentTypes.Job, MonetizationSubjectType.Job },
        { ModerationContentTypes.MarketplaceListing, MonetizationSubjectType.MarketplaceListing },
        { ModerationContentTypes.HousingListing, MonetizationSubjectType.HousingListing },
        { ModerationContentTypes.Provider, MonetizationSubjectType.Provider },
        { ModerationContentTypes.Event, MonetizationSubjectType.Event },
        { ModerationContentTypes.Organization, MonetizationSubjectType.Organization },
        { ModerationContentTypes.Guide, MonetizationSubjectType.Guide },
        { ModerationContentTypes.Fundraiser, null },
        { ModerationContentTypes.Media, null },
        { ModerationContentTypes.CommunityPost, null },
        { ModerationContentTypes.CommunityComment, null }
    };

    [Theory]
    [MemberData(nameof(ContentTypes))]
    public void Each_moderation_content_type_maps_to_the_placement_subject_it_names(
        string contentType, MonetizationSubjectType? expected) =>
        ModerationPlacementSubjects.Map(contentType).Should().Be(expected);

    /// <summary>
    /// A content type added to the moderation vocabulary is a decision here: either something sells
    /// placements over it and it maps, or it is listed above as mapping to nothing.
    /// </summary>
    [Fact]
    public void Every_moderation_content_type_is_decided()
    {
        var declared = typeof(ModerationContentTypes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        var decided = ContentTypes.Select(row => (string)row[0]).ToList();

        declared.Should().BeEquivalentTo(decided);
    }

    private static Placement Purchase(int days = 30, TimeSpan? startsIn = null) =>
        Placement.Purchase(
            PlacementId.New(),
            SubjectReference.Create(MonetizationSubjectType.Job, Guid.NewGuid()),
            PlacementKind.Boost,
            DateWindow.ForDays(Start + (startsIn ?? TimeSpan.Zero), days),
            1.5m,
            Guid.NewGuid(),
            "job.boost.7d",
            Start);
}
