using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Guides;

/// <summary>
/// The optimistic concurrency contract between the editor and the aggregate.
/// <para>
/// Concurrency here is a revision number, not a timestamp. An editor sends back the revision it
/// loaded; a save against a revision somebody else has moved past is refused as a
/// <see cref="Diaspora.SharedKernel.Results.ErrorType.Conflict"/> the client can offer a merge
/// from, rather than silently overwriting a co-author's paragraph.
/// </para>
/// <para>
/// <b>The half that is easy to get wrong is the other half.</b> A reader viewing a guide, a
/// vote landing, a bookmark count arriving from Community — none of those is an edit, and if any
/// of them bumped the revision then a popular guide would become unsaveable: the author's
/// in-flight save would lose a race against strangers reading their own work.
/// </para>
/// </summary>
public class GuideConcurrencyTests
{
    private static readonly DateTimeOffset Now = GuideLifecycleTests.Now;

    // ── The guard itself ────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_caller_that_names_no_revision_has_opted_out_of_the_check()
    {
        var guide = GuideLifecycleTests.Drafted();

        guide.EnsureRevision(null).IsSuccess.Should().BeTrue(
            "internal writes and last-writer-wins clients pass null deliberately");
    }

    [Fact]
    public void The_revision_the_editor_loaded_is_accepted()
    {
        var guide = GuideLifecycleTests.Drafted();

        guide.EnsureRevision(guide.Revision).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A_revision_the_editor_no_longer_holds_is_a_conflict()
    {
        var guide = GuideLifecycleTests.Drafted();

        var stale = guide.EnsureRevision(guide.Revision - 1);

        stale.IsFailure.Should().BeTrue();
        stale.Error.Should().Be(GuideErrors.RevisionConflict);
        stale.Error.Type.Should().Be(Diaspora.SharedKernel.Results.ErrorType.Conflict,
            "a conflict is what lets the editor offer a merge instead of an error page");
    }

    [Fact]
    public void A_revision_from_the_future_is_also_a_conflict()
    {
        var guide = GuideLifecycleTests.Drafted();

        guide.EnsureRevision(guide.Revision + 1).Error.Should().Be(GuideErrors.RevisionConflict);
    }

    // ── Content mutations bump it ───────────────────────────────────────────────────────────

    [Fact]
    public void Replacing_the_structure_bumps_the_revision()
    {
        var guide = GuideLifecycleTests.Drafted();
        var before = guide.Revision;

        guide.ReplaceStructure(
            GuideLifecycleTests.OneSection("Rewritten"), baseRevision: before)
            .IsSuccess.Should().BeTrue();

        guide.Revision.Should().Be(before + 1);
    }

    [Fact]
    public void A_save_against_the_revision_somebody_else_already_moved_past_is_refused()
    {
        var guide = GuideLifecycleTests.Drafted();

        var loadedByBoth = guide.Revision;

        // The first author saves.
        guide.ReplaceStructure(
            GuideLifecycleTests.OneSection("First author's wording"), baseRevision: loadedByBoth)
            .IsSuccess.Should().BeTrue();

        // The second author, who loaded the same revision, saves next.
        var second = guide.ReplaceStructure(
            GuideLifecycleTests.OneSection("Second author's wording"), baseRevision: loadedByBoth);

        second.IsFailure.Should().BeTrue();
        second.Error.Should().Be(GuideErrors.RevisionConflict);

        guide.Sections[0].Title.Should().Be("First author's wording",
            "the loser of the race must not have half-overwritten the winner");
    }

    [Fact]
    public void Every_metadata_mutator_honours_a_stale_revision()
    {
        var guide = GuideLifecycleTests.Drafted();
        var stale = guide.Revision - 1;

        guide.UpdateMetadata(
                GuideLifecycleTests.Title("A better title for the same guide"),
                summary: null,
                GuideSeoMetadata.Empty,
                GuideVisibility.Public,
                GuideDifficulty.Beginner,
                estimatedCompletionMinutes: null,
                GuideLifecycleTests.Location(),
                baseRevision: stale)
            .Error.Should().Be(GuideErrors.RevisionConflict);

        guide.SetTags(["banking"], baseRevision: stale)
            .Error.Should().Be(GuideErrors.RevisionConflict);

        guide.ChangeCategory("housing", baseRevision: stale)
            .Error.Should().Be(GuideErrors.RevisionConflict);

        guide.ChangeLanguage(GuideLifecycleTests.Language("fr"), baseRevision: stale)
            .Error.Should().Be(GuideErrors.RevisionConflict);

        guide.RestoreVersion(
                GuideLifecycleTests.Title(),
                summary: null,
                GuideLifecycleTests.Language(),
                guide.CategoryKey,
                GuideLifecycleTests.OneSection("Restored"),
                baseRevision: stale)
            .Error.Should().Be(GuideErrors.RevisionConflict);
    }

    [Fact]
    public void A_refused_save_changes_nothing_at_all()
    {
        var guide = GuideLifecycleTests.Drafted();

        var revision = guide.Revision;
        var title = guide.Title;

        guide.UpdateMetadata(
            GuideLifecycleTests.Title("A better title for the same guide"),
            summary: null,
            GuideSeoMetadata.Empty,
            GuideVisibility.Private,
            GuideDifficulty.Advanced,
            estimatedCompletionMinutes: 60,
            GuideLifecycleTests.Location("FR"),
            baseRevision: revision - 1).IsFailure.Should().BeTrue();

        guide.Revision.Should().Be(revision);
        guide.Title.Should().Be(title);
        guide.Visibility.Should().Be(GuideVisibility.Public);
        guide.Location.CountryCode.Should().Be("GB");
    }

    [Fact]
    public void A_metadata_update_that_changes_nothing_does_not_bump_the_revision()
    {
        // An idempotent save must not invalidate a co-author's editor session for no reason.
        var guide = GuideLifecycleTests.Drafted();
        var revision = guide.Revision;

        guide.UpdateMetadata(
            guide.Title,
            guide.Summary,
            guide.Seo,
            guide.Visibility,
            guide.Reading.Difficulty,
            guide.Reading.EstimatedCompletionMinutes,
            guide.Location,
            baseRevision: revision).IsSuccess.Should().BeTrue();

        guide.Revision.Should().Be(revision);
    }

    [Fact]
    public void The_lifecycle_transitions_bump_the_revision_too()
    {
        var guide = GuideLifecycleTests.Drafted();

        var beforePublish = guide.Revision;
        guide.Publish(Now).IsSuccess.Should().BeTrue();
        guide.Revision.Should().Be(beforePublish + 1);

        var beforeArchive = guide.Revision;
        guide.Archive(Now.AddDays(1)).IsSuccess.Should().BeTrue();
        guide.Revision.Should().Be(beforeArchive + 1);

        var beforeRestore = guide.Revision;
        guide.RestoreFromArchive(Now.AddDays(2)).IsSuccess.Should().BeTrue();
        guide.Revision.Should().Be(beforeRestore + 1);
    }

    [Fact]
    public void Attaching_and_detaching_media_bumps_the_revision()
    {
        var guide = GuideLifecycleTests.Drafted();
        var mediaId = Guid.NewGuid();

        var before = guide.Revision;

        guide.AttachMedia(mediaId, GuideMediaRole.Hero).IsSuccess.Should().BeTrue();
        guide.Revision.Should().Be(before + 1);

        guide.DetachMedia(mediaId, GuideMediaRole.Hero).IsSuccess.Should().BeTrue();
        guide.Revision.Should().Be(before + 2);
    }

    [Fact]
    public void An_idempotent_attach_or_detach_is_not_an_edit()
    {
        var guide = GuideLifecycleTests.Drafted();
        var mediaId = Guid.NewGuid();

        guide.AttachMedia(mediaId, GuideMediaRole.Hero).IsSuccess.Should().BeTrue();

        var revision = guide.Revision;

        guide.AttachMedia(mediaId, GuideMediaRole.Hero).IsSuccess.Should().BeTrue();
        guide.DetachMedia(Guid.NewGuid(), GuideMediaRole.Gallery).IsSuccess.Should().BeTrue();

        guide.Revision.Should().Be(revision,
            "a retried attach is the same attach, and a retry must be free");
    }

    // ── Counters do not ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Recording_a_view_is_not_an_edit()
    {
        var guide = GuideLifecycleTests.Published();
        guide.ClearDomainEvents();

        var revision = guide.Revision;

        guide.RecordView();
        guide.RecordView(unique: true);

        guide.Engagement.Views.Should().Be(2);
        guide.Engagement.UniqueViews.Should().Be(1);

        guide.Revision.Should().Be(revision,
            "a reader opening a guide must never make its author's in-flight save fail");

        guide.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void No_engagement_counter_bumps_the_revision_or_raises_an_event()
    {
        var guide = GuideLifecycleTests.Published();
        guide.ClearDomainEvents();

        var revision = guide.Revision;

        guide.RecordView();
        guide.ApplyVoteDelta(helpfulDelta: 1, notHelpfulDelta: 0);
        guide.ApplyBookmarkDelta(3);
        guide.ApplyCommentCount(12);
        guide.RecordShare();

        guide.Engagement.HelpfulCount.Should().Be(1);
        guide.Engagement.BookmarkCount.Should().Be(3);
        guide.Engagement.CommentCount.Should().Be(12);
        guide.Engagement.ShareCount.Should().Be(1);

        guide.Revision.Should().Be(revision,
            "a popular guide would otherwise become unsaveable by its own author");

        guide.DomainEvents.Should().BeEmpty(
            "counters are a projection of other aggregates, not a fact about this one");
    }

    [Fact]
    public void An_editor_saving_against_a_revision_survives_a_thousand_readers()
    {
        var guide = GuideLifecycleTests.Published();

        var loaded = guide.Revision;

        for (var reader = 0; reader < 1_000; reader++)
        {
            guide.RecordView(unique: reader % 3 == 0);
        }

        guide.ReplaceStructure(
                GuideLifecycleTests.OneSection("Corrected while people were reading"),
                baseRevision: loaded)
            .IsSuccess.Should().BeTrue(
                "views are not edits, so the author's save is still against the current revision");

        guide.Engagement.Views.Should().Be(1_000);
    }

    // ── Counters on a deleted or archived guide ─────────────────────────────────────────────

    [Fact]
    public void Counters_are_reachable_on_an_archived_guide_because_readers_still_arrive()
    {
        var guide = GuideLifecycleTests.Published();

        guide.Archive(Now.AddDays(1)).IsSuccess.Should().BeTrue();
        guide.ClearDomainEvents();

        var revision = guide.Revision;

        guide.RecordView();
        guide.RecordShare();

        guide.Engagement.Views.Should().Be(1);
        guide.Revision.Should().Be(revision);
        guide.DomainEvents.Should().BeEmpty();
    }
}
