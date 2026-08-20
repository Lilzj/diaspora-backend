using System.Reflection;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.Versions;
using FluentAssertions;
using Xunit;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.UnitTests.Guides;

/// <summary>
/// What a reader was shown, and the guarantee that it stays what they were shown.
/// <para>
/// A guide that once gave wrong advice must not be able to rewrite the record of having given
/// it, so a version has no mutators at all and a restore never touches one. The other half of
/// the promise is that history is complete: every publication mints a version, and the numbers
/// only ever go up — a re-used or skipped number makes "version 3" ambiguous forever, and
/// version numbers are what a moderation review and a support conversation both cite.
/// </para>
/// </summary>
public class GuideVersioningTests
{
    private static readonly DateTimeOffset Now = GuideLifecycleTests.Now;

    [Fact]
    public void Every_publication_announces_a_version_for_the_handler_to_snapshot()
    {
        var guide = GuideLifecycleTests.Drafted();

        guide.Publish(Now, GuideLifecycleTests.Editor).IsSuccess.Should().BeTrue();

        var created = guide.DomainEvents.OfType<GuideVersionCreatedDomainEvent>().Single();

        created.Version.Should().Be(1);
        created.EditorId.Should().Be(GuideLifecycleTests.Editor);
        created.PublishedAt.Should().Be(Now);
    }

    [Fact]
    public void The_publisher_defaults_to_the_author_when_nobody_else_is_named()
    {
        var guide = GuideLifecycleTests.Drafted();

        guide.Publish(Now).IsSuccess.Should().BeTrue();

        guide.DomainEvents.OfType<GuideVersionCreatedDomainEvent>().Single()
            .EditorId.Should().Be(GuideLifecycleTests.Author,
                "the version list attributes every version to somebody, always");
    }

    [Fact]
    public void Version_numbers_are_monotonic_across_a_long_editing_life()
    {
        var guide = GuideLifecycleTests.Drafted();

        var announced = new List<int>();

        for (var publication = 1; publication <= 5; publication++)
        {
            guide.ClearDomainEvents();

            guide.ReplaceStructure(
                GuideLifecycleTests.OneSection(content: $"Revision {publication} of the advice."),
                baseRevision: null).IsSuccess.Should().BeTrue();

            guide.Publish(Now.AddDays(publication)).IsSuccess.Should().BeTrue();

            announced.Add(guide.DomainEvents.OfType<GuideVersionCreatedDomainEvent>().Single().Version);
        }

        announced.Should().Equal(
            new[] { 1, 2, 3, 4, 5 },
            "a reused or skipped number makes 'version 3' ambiguous forever");

        guide.CurrentVersion.Should().Be(5);
    }

    [Fact]
    public void Archiving_and_restoring_mint_no_version_because_neither_changed_what_readers_see()
    {
        var guide = GuideLifecycleTests.Published();

        guide.ClearDomainEvents();

        guide.Archive(Now.AddDays(1)).IsSuccess.Should().BeTrue();
        guide.RestoreFromArchive(Now.AddDays(2)).IsSuccess.Should().BeTrue();

        guide.DomainEvents.OfType<GuideVersionCreatedDomainEvent>().Should().BeEmpty();
        guide.CurrentVersion.Should().Be(1);
    }

    [Fact]
    public void A_version_exposes_no_way_to_change_itself()
    {
        // The immutability is a design claim, so it is asserted against the type rather than
        // against one instance: a setter added tomorrow fails here rather than in an audit.
        var mutators = typeof(GuideVersion)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)

            // The persistence interceptor stamps audit columns on every entity; it is not a
            // way for a caller to rewrite what a reader was shown.
            .Where(method => method.Name is not ("SetCreated" or "SetModified"))
            .Select(method => method.Name)
            .ToList();

        mutators.Should().BeEmpty(
            "a version is a historical fact, and a historical fact has no mutators");

        typeof(GuideVersion)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(property => property.SetMethod?.IsPublic == true)
            .Should().BeEmpty("every member is private-set so only construction decides the content");
    }

    [Fact]
    public void A_version_records_the_content_it_published_and_nothing_it_can_recompute()
    {
        var guide = GuideLifecycleTests.Published(GuideLifecycleTests.Words(600));

        var version = GuideVersion.Create(
            GuideVersionId.New(),
            (GuideId)guide.Id,
            guide.CurrentVersion,
            guide.Title,
            summary: null,
            guide.Language,
            guide.CategoryKey,
            GuideSnapshotSerializer.Serialize(guide),
            "Corrected the appointment step.",
            GuideLifecycleTests.Author,
            Now,
            guide.WordCount,
            guide.Reading.ReadingTimeMinutes);

        version.IsSuccess.Should().BeTrue();
        version.Value.VersionNumber.Should().Be(1);
        version.Value.WordCount.Should().Be(600);
        version.Value.ReadingTimeMinutes.Should().Be(3);
        version.Value.RevisionNotes.Should().Be("Corrected the appointment step.");
        version.Value.ContentSnapshot.Should().Contain("Before you start");
    }

    [Fact]
    public void A_version_without_a_snapshot_is_refused()
    {
        var created = NewVersion(snapshot: "   ");

        created.IsFailure.Should().BeTrue();
        created.Error.Should().Be(GuideErrors.SnapshotInvalid,
            "an empty version is a hole in history that presents as a successful publication");
    }

    [Fact]
    public void A_version_number_below_one_is_refused()
    {
        NewVersion(versionNumber: 0).Error.Should().Be(GuideErrors.VersionNotFound);
        NewVersion(versionNumber: -1).Error.Should().Be(GuideErrors.VersionNotFound);
    }

    [Fact]
    public void Revision_notes_past_the_ceiling_are_refused_rather_than_truncated()
    {
        // Truncating an editor's explanation mid-sentence is worse than telling them it is
        // too long: the version list renders these to readers deciding whether to re-read.
        var created = NewVersion(notes: new string('n', GuideLimits.MaxRevisionNotes + 1));

        created.IsFailure.Should().BeTrue();
        created.Error.Should().Be(GuideErrors.RevisionNotesInvalid);
    }

    [Fact]
    public void A_versions_reading_time_is_never_below_one_minute()
    {
        NewVersion(readingTime: 0).Value.ReadingTimeMinutes.Should().Be(1);
        NewVersion(wordCount: -5).Value.WordCount.Should().Be(0);
    }

    [Fact]
    public void Restoring_a_version_applies_its_content_onto_the_draft()
    {
        var guide = GuideLifecycleTests.Published(
            GuideLifecycleTests.OneSection("Step one", "Book the appointment online."));

        var snapshot = GuideSnapshotSerializer.Serialize(guide);

        // The author rewrites, publishes again, then changes their mind.
        guide.ReplaceStructure(
            GuideLifecycleTests.OneSection("Rewritten", "Walk in without an appointment."),
            baseRevision: null).IsSuccess.Should().BeTrue();

        guide.Publish(Now.AddDays(1)).IsSuccess.Should().BeTrue();
        guide.CurrentVersion.Should().Be(2);

        var drafts = GuideSnapshotSerializer.ToDrafts(
            GuideSnapshotSerializer.Deserialize(snapshot).Value);

        var restored = guide.RestoreVersion(
            GuideLifecycleTests.Title(),
            summary: null,
            GuideLifecycleTests.Language(),
            guide.CategoryKey,
            drafts.Value,
            baseRevision: null,
            GuideLifecycleTests.Author);

        restored.IsSuccess.Should().BeTrue();
        guide.Sections.Should().ContainSingle().Which.Title.Should().Be("Step one");
        guide.Sections[0].Blocks[0].Content.Should().Be("Book the appointment online.");
    }

    [Fact]
    public void Restoring_a_version_does_not_move_the_published_version_number()
    {
        var guide = GuideLifecycleTests.Published();

        guide.Publish(Now.AddDays(1)).IsSuccess.Should().BeTrue();
        guide.CurrentVersion.Should().Be(2);

        guide.RestoreVersion(
            GuideLifecycleTests.Title(),
            summary: null,
            GuideLifecycleTests.Language(),
            guide.CategoryKey,
            GuideLifecycleTests.OneSection("Restored", "The older wording."),
            baseRevision: null).IsSuccess.Should().BeTrue();

        guide.CurrentVersion.Should().Be(2,
            "a restore is an ordinary draft edit — readers of version 2 are untouched until the " +
            "author publishes again");

        guide.Publish(Now.AddDays(2)).IsSuccess.Should().BeTrue();

        guide.CurrentVersion.Should().Be(3,
            "restoring version 1 mints version 3; it does not travel backwards");
    }

    [Fact]
    public void Restoring_a_version_raises_a_content_update_and_no_publication()
    {
        var guide = GuideLifecycleTests.Published();
        guide.ClearDomainEvents();

        guide.RestoreVersion(
            GuideLifecycleTests.Title(),
            summary: null,
            GuideLifecycleTests.Language(),
            guide.CategoryKey,
            GuideLifecycleTests.OneSection("Restored", "The older wording."),
            baseRevision: null).IsSuccess.Should().BeTrue();

        guide.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<GuideContentUpdatedDomainEvent>();

        guide.DomainEvents.OfType<GuidePublishedDomainEvent>().Should().BeEmpty();
    }

    [Fact]
    public void Restoring_an_empty_or_invalid_snapshot_leaves_the_draft_alone()
    {
        var guide = GuideLifecycleTests.Published(
            GuideLifecycleTests.OneSection("Step one", "Book the appointment online."));

        var restored = guide.RestoreVersion(
            GuideLifecycleTests.Title(),
            summary: null,
            GuideLifecycleTests.Language(),
            guide.CategoryKey,
            sections: [],
            baseRevision: null);

        restored.IsFailure.Should().BeTrue();
        restored.Error.Should().Be(GuideErrors.StructureEmpty);

        guide.Sections.Should().ContainSingle().Which.Title.Should().Be("Step one",
            "a rejected restore must not have half-applied itself");
    }

    private static Diaspora.SharedKernel.Results.Result<GuideVersion> NewVersion(
        int versionNumber = 1,
        string? snapshot = "{\"schemaVersion\":1,\"sections\":[]}",
        string? notes = null,
        int wordCount = 100,
        int readingTime = 1) =>
        GuideVersion.Create(
            GuideVersionId.New(),
            GuideId.New(),
            versionNumber,
            GuideLifecycleTests.Title(),
            summary: null,
            GuideLifecycleTests.Language(),
            GuideLifecycleTests.CategoryKey,
            snapshot,
            notes,
            GuideLifecycleTests.Author,
            GuideLifecycleTests.Now,
            wordCount,
            readingTime);
}
