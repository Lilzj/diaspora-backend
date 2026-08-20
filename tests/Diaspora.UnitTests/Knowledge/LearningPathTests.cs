using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.LearningPaths;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Knowledge;

/// <summary>
/// A curated route through one country's knowledge.
/// <para>
/// <b>Curated, and the brief is explicit that it stays that way.</b> Nothing here infers a sequence.
/// A person who knows what it is like to arrive somewhere with two suitcases decides that
/// immigration comes before housing and housing before banking, and writes it down — that ordering
/// is knowledge, and it is exactly the kind this platform exists to carry.
/// </para>
/// <para>
/// <b>Step identity survives an insertion, and that is load-bearing.</b> A client tracking "you are
/// on step 3" tracks it against the identifier, so an editor inserting a step above must not
/// silently advance every reader to the next one.
/// </para>
/// </summary>
public class LearningPathTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static readonly UserId Curator = UserId.From(Guid.NewGuid());

    // ── Creation ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_created_path_is_a_draft_with_no_steps()
    {
        var path = Create("arriving", "MU", "Arriving in Mauritius");

        path.IsSuccess.Should().BeTrue();
        path.Value.Status.Should().Be(LearningPathStatus.Draft);
        path.Value.Steps.Should().BeEmpty();
        path.Value.IsPublishable.Should().BeFalse("a path with no steps is a title");
    }

    /// <summary>
    /// The country is half the key, so <c>arriving</c> works for every country at once rather than
    /// forcing curators to invent <c>arriving-mu</c> and put the country in the URL twice.
    /// </summary>
    [Fact]
    public void The_same_key_is_available_in_every_country()
    {
        Create("arriving", "MU", "Arriving in Mauritius").Value.Key.Should().Be("arriving");
        Create("arriving", "CA", "Arriving in Canada").Value.Key.Should().Be("arriving");
    }

    [Theory]
    [InlineData("has space")]
    [InlineData(null)]
    public void A_key_that_could_not_appear_in_a_url_is_refused(string? key) =>
        Create(key, "MU", "Arriving").Error.Should().Be(KnowledgeErrors.PathKeyInvalid);

    [Fact]
    public void A_path_without_a_country_is_refused() =>
        Create("arriving", "MUS", "Arriving").Error.Should().Be(KnowledgeErrors.CountryInvalid);

    // ── Steps ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>A step that points at nothing is a heading, and a heading in a numbered sequence is a step
    /// a reader cannot complete.</b> Refusing it here means no progress indicator on the platform can
    /// ever divide by a denominator that includes one.
    /// </summary>
    [Fact]
    public void A_step_that_points_at_neither_a_guide_nor_a_category_is_refused()
    {
        var path = Create("arriving", "MU", "Arriving").Value;

        var result = path.Arrange(
            [(Guid.NewGuid(), "Get oriented", null, null, null, false)], Curator);

        result.Error.Should().Be(KnowledgeErrors.StepInvalid);
    }

    [Fact]
    public void A_step_may_point_at_a_guide_at_a_category_or_at_both()
    {
        var path = Create("arriving", "MU", "Arriving").Value;

        var guide = Guid.NewGuid();

        var result = path.Arrange(
            [
                (Guid.NewGuid(), "Read this first", null, guide, null, false),
                (Guid.NewGuid(), "Sort out your banking", null, null, "banking", false),
                (Guid.NewGuid(), "Find somewhere to live", null, guide, "housing", false)
            ],
            Curator);

        result.IsSuccess.Should().BeTrue();
        path.Steps.Should().HaveCount(3);
        path.Steps.Select(step => step.Position).Should().BeEquivalentTo([0, 1, 2]);
    }

    /// <summary>
    /// The step's own title, not the guide's. "Sort out your right to be here" is the step;
    /// "Applying for an occupation permit" is the guide, and they read very differently.
    /// </summary>
    [Fact]
    public void A_step_carries_its_own_framing_rather_than_the_guides_title()
    {
        var path = Arranged(("Sort out your right to be here", "immigration", false));

        path.Steps[0].Title.Should().Be("Sort out your right to be here");
    }

    [Fact]
    public void A_step_identifier_survives_an_insertion_above_it()
    {
        var path = Create("arriving", "MU", "Arriving").Value;

        var housing = Guid.NewGuid();

        path.Arrange([(housing, "Find somewhere to live", null, null, "housing", false)], Curator);

        path.Arrange(
            [
                (Guid.NewGuid(), "Sort out your papers", null, null, "immigration", false),
                (housing, "Find somewhere to live", null, null, "housing", false)
            ],
            Curator);

        var moved = path.Steps.Single(step => step.StepId == housing);

        moved.Position.Should().Be(1, "it moved down");
        moved.StepId.Should().Be(housing, "but a client tracking progress still recognises it");
    }

    [Fact]
    public void The_same_step_identifier_submitted_twice_is_refused()
    {
        var path = Create("arriving", "MU", "Arriving").Value;
        var stepId = Guid.NewGuid();

        var result = path.Arrange(
            [
                (stepId, "One", null, null, "immigration", false),
                (stepId, "Two", null, null, "housing", false)
            ],
            Curator);

        result.Error.Should().Be(KnowledgeErrors.StepInvalid);
    }

    /// <summary>
    /// Optional steps keep a path honest for the people it half applies to: registering children for
    /// school is essential for a family and irrelevant to a single graduate, and a path that told the
    /// graduate they were permanently 6 of 7 done is a path they abandon.
    /// </summary>
    [Fact]
    public void An_optional_step_does_not_count_towards_completion()
    {
        var path = Arranged(
            ("Papers", "immigration", false),
            ("Schools for the children", "education", true),
            ("Banking", "banking", false));

        path.Steps.Should().HaveCount(3);
        path.RequiredStepCount.Should().Be(2);
    }

    [Fact]
    public void More_steps_than_a_path_can_hold_are_refused()
    {
        var path = Create("arriving", "MU", "Arriving").Value;

        var many = Enumerable.Range(0, 40)
            .Select(index =>
                (Guid.NewGuid(), (string?)$"Step {index}", (string?)null, (Guid?)null,
                    (string?)"immigration", false))
            .ToList();

        path.Arrange(many, Curator).Error.Should().Be(KnowledgeErrors.StepLimit);
    }

    // ── Lifecycle ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_path_with_no_steps_cannot_be_published()
    {
        var path = Create("arriving", "MU", "Arriving").Value;

        path.Publish(Now, Curator).Error.Should().Be(KnowledgeErrors.PathNotPublishable);
    }

    [Fact]
    public void A_path_with_steps_publishes_and_stamps_the_instant()
    {
        var path = Arranged(("Papers", "immigration", false));

        path.Publish(Now, Curator).IsSuccess.Should().BeTrue();

        path.Status.Should().Be(LearningPathStatus.Published);
        path.PublishedAt.Should().Be(Now);

        path.Publish(Now, Curator).Error.Should().Be(KnowledgeErrors.PathAlreadyPublished);
    }

    /// <summary>
    /// <b>Rearranging a published path is allowed, deliberately.</b> Unpublish-edit-republish takes a
    /// country's onboarding offline for as long as the editor takes, and the commonest edit by far is
    /// inserting a step somebody discovered was missing. A reader mid-path sees a longer path, which
    /// is exactly what happened.
    /// </summary>
    [Fact]
    public void A_published_path_can_be_rearranged_without_going_offline()
    {
        var path = Arranged(("Papers", "immigration", false));
        path.Publish(Now, Curator);

        var result = path.Arrange(
            [
                (Guid.NewGuid(), "Papers", null, null, "immigration", false),
                (Guid.NewGuid(), "Banking", null, null, "banking", false)
            ],
            Curator);

        result.IsSuccess.Should().BeTrue();
        path.Status.Should().Be(LearningPathStatus.Published);
        path.Steps.Should().HaveCount(2);
    }

    [Fact]
    public void An_archived_path_cannot_be_rearranged_until_it_is_restored()
    {
        var path = Arranged(("Papers", "immigration", false));
        path.Archive(Curator);

        path.Arrange([(Guid.NewGuid(), "Papers", null, null, "immigration", false)], Curator)
            .Error.Should().Be(KnowledgeErrors.PathArchived);

        path.Restore(Curator).IsSuccess.Should().BeTrue();
        path.Status.Should().Be(LearningPathStatus.Draft, "never straight back to published");

        path.Arrange([(Guid.NewGuid(), "Papers", null, null, "immigration", false)], Curator)
            .IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// The steps stay. A path that was correct until a country changed its visa rules records what
    /// the advice used to be, and the platform keeps those.
    /// </summary>
    [Fact]
    public void Archiving_keeps_the_steps()
    {
        var path = Arranged(("Papers", "immigration", false), ("Banking", "banking", false));

        path.Archive(Curator);

        path.Status.Should().Be(LearningPathStatus.Archived);
        path.Steps.Should().HaveCount(2);
    }

    [Fact]
    public void Every_change_raises_the_event_a_progress_indicator_depends_on()
    {
        var path = Arranged(("Papers", "immigration", false));
        path.ClearDomainEvents();

        path.Publish(Now, Curator);

        var raised = path.DomainEvents.OfType<LearningPathChangedDomainEvent>().Single();

        raised.Change.Should().Be(KnowledgeChangeKinds.Published);
        raised.StepCount.Should().Be(1, "the denominator of every progress indicator");
        raised.CountryCode.Should().Be("MU");
        raised.Status.Should().Be(LearningPathStatus.Published);
    }

    [Fact]
    public void A_write_from_a_stale_revision_is_refused()
    {
        var path = Arranged(("Papers", "immigration", false));

        path.EnsureRevision(0).Error.Should().Be(KnowledgeErrors.RevisionConflict);
        path.EnsureRevision(path.Revision).IsSuccess.Should().BeTrue();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static Result<LearningPath> Create(string? key, string? country, string? title) =>
        LearningPath.Create(
            LearningPathId.New(), key, country, title, Now, createdBy: Curator);

    private static LearningPath Arranged(
        params (string Title, string CategoryKey, bool IsOptional)[] steps)
    {
        var path = Create("arriving", "MU", "Arriving in Mauritius").Value;

        path.Arrange(
            [
                .. steps.Select(step =>
                    (Guid.NewGuid(), (string?)step.Title, (string?)null, (Guid?)null,
                        (string?)step.CategoryKey, step.IsOptional))
            ],
            Curator);

        return path;
    }
}
