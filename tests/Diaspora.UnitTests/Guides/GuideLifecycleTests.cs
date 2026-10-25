using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.UnitTests.Guides;

/// <summary>
/// The publication lifecycle of a guide, which is the module's central promise.
/// <para>
/// The aggregate is always the draft; publication stamps a version onto it rather than copying
/// content anywhere. Every case here is a way a naive implementation puts half-written
/// procedural advice in front of members who will act on it — an empty guide in search, an
/// archived guide silently accepting edits nobody will ever read, a schedule that fires into
/// the past.
/// </para>
/// </summary>
public class GuideLifecycleTests
{
    internal static readonly DateTimeOffset Now = new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);

    internal static readonly UserId Author = UserId.From(Guid.Parse("00000000-0000-0000-0000-0000000000b1"));
    internal static readonly UserId Editor = UserId.From(Guid.Parse("00000000-0000-0000-0000-0000000000b2"));
    internal static readonly UserId Reader = UserId.From(Guid.Parse("00000000-0000-0000-0000-0000000000b3"));
    internal static readonly UserId Moderator = UserId.From(Guid.Parse("00000000-0000-0000-0000-0000000000b4"));

    internal const string CategoryKey = "banking";

    [Fact]
    public void A_created_guide_is_a_draft_that_nothing_can_discover()
    {
        var guide = Draft();

        guide.Status.Should().Be(GuideStatus.Draft);
        guide.IsDiscoverable.Should().BeFalse(
            "a half-written procedure must not reach a member who would act on it");
        guide.CurrentVersion.Should().Be(0);
        guide.PublishedAt.Should().BeNull();
        guide.FirstPublishedAt.Should().BeNull();
        guide.TrustMark.State.Should().Be(GuideVerificationState.None);
    }

    [Fact]
    public void A_created_guide_is_its_own_translation_group_so_linking_is_joining()
    {
        var id = GuideId.New();

        var guide = Draft(id);

        guide.TranslationGroupId.Should().Be(id.Value,
            "the group always exists, so adding a translation is joining rather than creating");
    }

    [Fact]
    public void Creation_refuses_a_category_key_that_is_not_a_key()
    {
        var created = Guide.Create(
            GuideId.New(),
            Author,
            Title(),
            Slug(),
            "Not A Key",
            Language(),
            Location(),
            Now);

        created.IsFailure.Should().BeTrue();
        created.Error.Should().Be(GuideErrors.CategoryKeyInvalid);
    }

    [Fact]
    public void Publishing_an_empty_guide_is_refused()
    {
        var guide = Draft();

        guide.IsPublishable.Should().BeFalse();

        var published = guide.Publish(Now);

        published.IsFailure.Should().BeTrue();
        published.Error.Should().Be(GuideErrors.NotPublishable,
            "an empty guide in search results is worse than no guide at all");
        guide.Status.Should().Be(GuideStatus.Draft);
    }

    [Fact]
    public void A_guide_whose_only_section_is_empty_is_still_not_publishable()
    {
        var guide = Draft();

        guide.ReplaceStructure(
            [
                new GuideSectionDraft
                {
                    SectionId = Guid.NewGuid(),
                    Title = "Before you start",
                    Order = 0,
                    Blocks = []
                }
            ],
            baseRevision: null).IsSuccess.Should().BeTrue();

        guide.IsPublishable.Should().BeFalse(
            "a titled section with nothing under it is a heading, not a guide");

        guide.Publish(Now).Error.Should().Be(GuideErrors.NotPublishable);
    }

    [Fact]
    public void Publishing_stamps_the_first_version_and_makes_the_guide_discoverable()
    {
        var guide = Drafted();

        var published = guide.Publish(Now);

        published.IsSuccess.Should().BeTrue();
        guide.Status.Should().Be(GuideStatus.Published);
        guide.CurrentVersion.Should().Be(1, "version numbering starts at one, not zero");
        guide.PublishedAt.Should().Be(Now);
        guide.FirstPublishedAt.Should().Be(Now);
        guide.IsDiscoverable.Should().BeTrue();
    }

    [Fact]
    public void Publishing_computes_the_reading_time_at_the_platforms_one_speed()
    {
        // 600 words at 200 words per minute is three minutes. The number's job is
        // comparability across guides, so it is computed and never entered.
        var guide = Drafted(Words(600));

        guide.WordCount.Should().Be(600);

        guide.Publish(Now).IsSuccess.Should().BeTrue();

        guide.Reading.ReadingTimeMinutes.Should().Be(600 / GuideLimits.WordsPerMinute);
    }

    [Fact]
    public void A_very_short_guide_still_reads_for_one_minute()
    {
        var guide = Drafted(Words(10));

        guide.Publish(Now).IsSuccess.Should().BeTrue();

        guide.Reading.ReadingTimeMinutes.Should().Be(1, "'0 min read' is a lie, not a measurement");
    }

    [Fact]
    public void Republishing_keeps_the_first_publication_date_and_flags_only_the_first_as_first()
    {
        var guide = Drafted();

        guide.Publish(Now).IsSuccess.Should().BeTrue();

        var later = Now.AddDays(30);
        guide.ClearDomainEvents();

        guide.Publish(later).IsSuccess.Should().BeTrue();

        guide.FirstPublishedAt.Should().Be(Now, "the guide's age is when it first helped somebody");
        guide.PublishedAt.Should().Be(later);
        guide.CurrentVersion.Should().Be(2);

        guide.DomainEvents.OfType<GuidePublishedDomainEvent>().Single()
            .IsFirstPublication.Should().BeFalse();
    }

    [Fact]
    public void Archiving_retires_a_published_guide_from_every_public_surface()
    {
        var guide = Published();

        var archived = guide.Archive(Now.AddDays(1));

        archived.IsSuccess.Should().BeTrue();
        guide.Status.Should().Be(GuideStatus.Archived);
        guide.ArchivedAt.Should().Be(Now.AddDays(1));
        guide.IsDiscoverable.Should().BeFalse();
        guide.CurrentVersion.Should().Be(1, "archiving is not a publication and mints no version");
    }

    /// <summary>
    /// Trust takes back what a guide earned when anyone but its author archives it (D-9) and leaves
    /// it when the author does (Q4). It can only tell them apart if the event says who it was.
    /// </summary>
    [Fact]
    public void Archiving_says_who_did_it_when_a_person_did()
    {
        var byPerson = Published();
        var archivist = UserId.From(Guid.NewGuid());
        byPerson.ClearDomainEvents();

        byPerson.Archive(Now.AddDays(1), archivist).IsSuccess.Should().BeTrue();

        byPerson.DomainEvents.OfType<GuideArchivedDomainEvent>().Single()
            .ArchivedBy.Should().Be(archivist);

        var byModeration = Published();
        byModeration.ClearDomainEvents();

        byModeration.Archive(Now.AddDays(1)).IsSuccess.Should().BeTrue();

        byModeration.DomainEvents.OfType<GuideArchivedDomainEvent>().Single()
            .ArchivedBy.Should().BeNull("moderation names no person");
    }

    [Fact]
    public void A_draft_cannot_be_archived()
    {
        var guide = Drafted();

        var archived = guide.Archive(Now);

        archived.IsFailure.Should().BeTrue();
        archived.Error.Should().Be(GuideErrors.NotPublished);
    }

    [Fact]
    public void Restoring_from_archive_returns_the_guide_exactly_as_it_was()
    {
        var guide = Published();

        guide.Archive(Now.AddDays(1)).IsSuccess.Should().BeTrue();

        var restored = guide.RestoreFromArchive(Now.AddDays(2));

        restored.IsSuccess.Should().BeTrue();
        guide.Status.Should().Be(GuideStatus.Published);
        guide.ArchivedAt.Should().BeNull();
        guide.IsDiscoverable.Should().BeTrue();
        guide.CurrentVersion.Should().Be(1, "a restore is not a republication");
    }

    [Fact]
    public void Only_an_archived_guide_can_be_restored_from_archive()
    {
        var guide = Published();

        var restored = guide.RestoreFromArchive(Now);

        restored.IsFailure.Should().BeTrue();
        restored.Error.Should().Be(GuideErrors.NotArchived);
    }

    [Fact]
    public void An_archived_guide_refuses_every_content_edit()
    {
        var guide = Published();

        guide.Archive(Now.AddDays(1)).IsSuccess.Should().BeTrue();

        // Editing what readers cannot see would silently discard the author's work, and worse,
        // would let an archived guide come back changed without anyone reviewing the change.
        guide.ReplaceStructure(OneSection("Rewritten"), baseRevision: null)
            .Error.Should().Be(GuideErrors.ArchivedImmutable);

        guide.SetTags(["banking"], baseRevision: null)
            .Error.Should().Be(GuideErrors.ArchivedImmutable);

        guide.ChangeCategory("housing", baseRevision: null)
            .Error.Should().Be(GuideErrors.ArchivedImmutable);

        guide.AttachMedia(Guid.NewGuid(), GuideMediaRole.Hero)
            .Error.Should().Be(GuideErrors.ArchivedImmutable);

        guide.Publish(Now.AddDays(2))
            .Error.Should().Be(GuideErrors.ArchivedImmutable);
    }

    [Fact]
    public void Soft_deleting_raises_an_event_so_downstream_indexes_learn_of_it()
    {
        var guide = Published();
        guide.ClearDomainEvents();

        guide.MarkDeleted(Now.AddDays(3), Author.Value.ToString());

        guide.IsDeleted.Should().BeTrue();
        guide.DeletedAt.Should().Be(Now.AddDays(3));
        guide.IsDiscoverable.Should().BeFalse();

        guide.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<GuideDeletedDomainEvent>(
                "a delete that happens only in the change tracker cannot de-index anything");
    }

    [Fact]
    public void A_deleted_guide_refuses_every_mutation_until_it_is_undeleted()
    {
        var guide = Published();

        guide.MarkDeleted(Now, null);

        guide.ReplaceStructure(OneSection(), baseRevision: null).Error.Should().Be(GuideErrors.Deleted);
        guide.Publish(Now).Error.Should().Be(GuideErrors.Deleted);
        guide.Archive(Now).Error.Should().Be(GuideErrors.Deleted);
        guide.RestoreFromArchive(Now).Error.Should().Be(GuideErrors.Deleted);
        guide.Feature(Now, Moderator).Error.Should().Be(GuideErrors.Deleted);
        guide.SubmitForVerification(Now, Author).Error.Should().Be(GuideErrors.Deleted);
    }

    [Fact]
    public void Undeleting_resumes_the_status_the_guide_held()
    {
        var guide = Published();

        guide.MarkDeleted(Now, null);
        guide.ClearDomainEvents();

        guide.Restore();

        guide.IsDeleted.Should().BeFalse();
        guide.DeletedAt.Should().BeNull();
        guide.DeletedBy.Should().BeNull();
        guide.Status.Should().Be(GuideStatus.Published, "undelete is not republication");
        guide.IsDiscoverable.Should().BeTrue();

        guide.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<GuideUndeletedDomainEvent>();
    }

    [Fact]
    public void A_publication_cannot_be_scheduled_into_the_past()
    {
        var guide = Drafted();

        var scheduled = guide.SchedulePublication(Now.AddMinutes(-1), Now);

        scheduled.IsFailure.Should().BeTrue();
        scheduled.Error.Should().Be(GuideErrors.ScheduleInPast,
            "a schedule in the past either fires immediately or never, and neither is what was asked");
        guide.ScheduledPublishAt.Should().BeNull();
    }

    [Fact]
    public void Scheduling_for_the_current_instant_is_also_refused()
    {
        var guide = Drafted();

        guide.SchedulePublication(Now, Now).Error.Should().Be(GuideErrors.ScheduleInPast);
    }

    [Fact]
    public void Only_a_draft_can_be_scheduled()
    {
        var guide = Published();

        var scheduled = guide.SchedulePublication(Now.AddDays(1), Now);

        scheduled.IsFailure.Should().BeTrue();
        scheduled.Error.Should().Be(GuideErrors.NotDraft,
            "scheduling a live guide to publish is a no-op dressed as an intention");
    }

    [Fact]
    public void Scheduling_records_the_instant_and_announces_it()
    {
        var guide = Drafted();
        guide.ClearDomainEvents();

        var at = Now.AddDays(2);

        guide.SchedulePublication(at, Now).IsSuccess.Should().BeTrue();

        guide.ScheduledPublishAt.Should().Be(at);
        guide.DomainEvents.OfType<GuideScheduledDomainEvent>().Single()
            .ScheduledFor.Should().Be(at);
    }

    [Fact]
    public void Publishing_clears_a_pending_schedule_so_it_cannot_fire_twice()
    {
        var guide = Drafted();

        guide.SchedulePublication(Now.AddDays(2), Now).IsSuccess.Should().BeTrue();
        guide.Publish(Now.AddHours(1)).IsSuccess.Should().BeTrue();

        guide.ScheduledPublishAt.Should().BeNull(
            "a schedule left behind would publish an already-published guide a second time");
    }

    [Fact]
    public void Cancelling_a_schedule_that_does_not_exist_is_refused()
    {
        var guide = Drafted();

        var cancelled = guide.CancelScheduledPublication();

        cancelled.IsFailure.Should().BeTrue();
        cancelled.Error.Should().Be(GuideErrors.NotScheduled);
    }

    [Fact]
    public void Cancelling_a_schedule_leaves_the_guide_a_draft()
    {
        var guide = Drafted();

        guide.SchedulePublication(Now.AddDays(2), Now).IsSuccess.Should().BeTrue();
        guide.ClearDomainEvents();

        guide.CancelScheduledPublication().IsSuccess.Should().BeTrue();

        guide.ScheduledPublishAt.Should().BeNull();
        guide.Status.Should().Be(GuideStatus.Draft);
        guide.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<GuideScheduleCancelledDomainEvent>();
    }

    [Fact]
    public void An_unlisted_guide_is_published_but_not_discoverable()
    {
        var guide = Published();

        guide.UpdateMetadata(
            Title(),
            summary: null,
            GuideSeoMetadata.Empty,
            GuideVisibility.Unlisted,
            GuideDifficulty.Beginner,
            estimatedCompletionMinutes: null,
            Location(),
            baseRevision: null).IsSuccess.Should().BeTrue();

        guide.Status.Should().Be(GuideStatus.Published);
        guide.IsDiscoverable.Should().BeFalse(
            "unlisted means reachable by link only, which is a different thing from published");
    }

    [Fact]
    public void A_hidden_guide_stops_being_discoverable_without_changing_its_own_state()
    {
        var guide = Published();

        guide.Hide("Advice contradicts the current visa rules.", Moderator, Now)
            .IsSuccess.Should().BeTrue();

        guide.ModerationState.Should().Be(GuideModerationState.Hidden);
        guide.Status.Should().Be(GuideStatus.Published, "hiding is reversible and touches nothing else");
        guide.IsDiscoverable.Should().BeFalse();

        guide.Unhide(Moderator).IsSuccess.Should().BeTrue();

        guide.IsDiscoverable.Should().BeTrue("lifting the decision restores exactly what was there");
    }

    [Fact]
    public void Only_a_published_guide_can_be_featured()
    {
        var guide = Drafted();

        guide.Feature(Now, Moderator).Error.Should().Be(GuideErrors.NotPublished);

        guide.Publish(Now).IsSuccess.Should().BeTrue();

        guide.Feature(Now, Moderator).IsSuccess.Should().BeTrue();
        guide.IsFeatured.Should().BeTrue();
        guide.FeaturedAt.Should().Be(Now);
    }

    // ── Shared builders, reused by every guide suite ─────────────────────────────────────────

    internal static GuideTitle Title(string value = "How to open a UK bank account") =>
        GuideTitle.Create(value).Value;

    internal static GuideSlug Slug(string value = "how-to-open-a-uk-bank-account") =>
        GuideSlug.Create(value).Value;

    internal static GuideLanguage Language(string code = "en") => GuideLanguage.Create(code).Value;

    internal static GuideLocationContext Location(string countryCode = "GB") =>
        GuideLocationContext.Create(countryCode).Value;

    /// <summary>A brand new, structureless draft.</summary>
    internal static Guide Draft(GuideId? id = null) =>
        Guide.Create(
            id ?? GuideId.New(),
            Author,
            Title(),
            Slug(),
            CategoryKey,
            Language(),
            Location(),
            Now).Value;

    /// <summary>A draft with content — the smallest guide that may legally be published.</summary>
    internal static Guide Drafted(IReadOnlyList<GuideSectionDraft>? sections = null)
    {
        var guide = Draft();

        guide.ReplaceStructure(sections ?? OneSection(), baseRevision: null)
            .IsSuccess.Should().BeTrue();

        return guide;
    }

    /// <summary>A published guide at version one.</summary>
    internal static Guide Published(IReadOnlyList<GuideSectionDraft>? sections = null)
    {
        var guide = Drafted(sections);

        guide.Publish(Now).IsSuccess.Should().BeTrue();

        return guide;
    }

    internal static IReadOnlyList<GuideSectionDraft> OneSection(
        string title = "Before you start",
        string content = "Bring your passport and a proof of address.") =>
        [
            new GuideSectionDraft
            {
                SectionId = Guid.NewGuid(),
                Title = title,
                Order = 0,
                Blocks =
                [
                    new GuideBlockDraft
                    {
                        BlockId = Guid.NewGuid(),
                        Kind = GuideBlockKind.Markdown,
                        Order = 0,
                        Content = content
                    }
                ]
            }
        ];

    /// <summary>One section whose prose is exactly <paramref name="count"/> words.</summary>
    internal static IReadOnlyList<GuideSectionDraft> Words(int count) =>
        OneSection(content: string.Join(' ', Enumerable.Repeat("word", count)));
}
