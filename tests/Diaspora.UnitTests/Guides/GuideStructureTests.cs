using System.Diagnostics;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Diaspora.UnitTests.Guides;

/// <summary>
/// The document structure, and the invariants that make a wholesale save safe.
/// <para>
/// The editor saves the whole document, so the aggregate validates the sections as a set and
/// rebuilds the owned graph from them. That is what makes the count and identity rules
/// trivially correct — and it is also what makes a partial application catastrophic, so the
/// tests here care as much about a rejected save leaving the previous structure intact as they
/// do about the rules themselves.
/// </para>
/// <para>
/// The last test in this file is large. It is a <b>correctness test that happens to be
/// large</b>: it builds a guide at the section and block ceilings and asserts that publication,
/// word counting and snapshot round-tripping are all still exactly right at that size. It
/// prints timings for the performance report and asserts on none of them, because a loaded
/// build agent must never be able to turn this suite red for reasons unrelated to the code.
/// </para>
/// </summary>
public class GuideStructureTests
{
    private readonly ITestOutputHelper _output;

    public GuideStructureTests(ITestOutputHelper output) => _output = output;

    // ── The set rules ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_guide_needs_at_least_one_section()
    {
        var guide = GuideLifecycleTests.Draft();

        var replaced = guide.ReplaceStructure([], baseRevision: null);

        replaced.IsFailure.Should().BeTrue();
        replaced.Error.Should().Be(GuideErrors.StructureEmpty);
    }

    [Fact]
    public void Exactly_the_maximum_number_of_sections_is_allowed()
    {
        var guide = GuideLifecycleTests.Draft();

        guide.ReplaceStructure(Sections(GuideLimits.MaxSections), baseRevision: null)
            .IsSuccess.Should().BeTrue("the ceiling is a bound, not an off-by-one");

        guide.Sections.Should().HaveCount(GuideLimits.MaxSections);
    }

    [Fact]
    public void One_section_past_the_ceiling_is_refused()
    {
        var guide = GuideLifecycleTests.Draft();

        var replaced = guide.ReplaceStructure(
            Sections(GuideLimits.MaxSections + 1), baseRevision: null);

        replaced.IsFailure.Should().BeTrue();
        replaced.Error.Should().Be(GuideErrors.SectionLimit,
            "the ceiling bounds how much work loading one guide can be");
    }

    [Fact]
    public void Two_sections_may_not_share_an_identifier()
    {
        var guide = GuideLifecycleTests.Draft();
        var shared = Guid.NewGuid();

        var replaced = guide.ReplaceStructure(
            [Section(shared, "First", 0), Section(shared, "Second", 1)],
            baseRevision: null);

        replaced.IsFailure.Should().BeTrue();
        replaced.Error.Should().Be(GuideErrors.SectionInvalid,
            "the identifier is what a version diff matches on; two sections sharing one make " +
            "'renamed' and 'replaced' indistinguishable");
    }

    [Fact]
    public void Two_blocks_may_not_share_an_identifier_even_across_sections()
    {
        var guide = GuideLifecycleTests.Draft();
        var shared = Guid.NewGuid();

        var replaced = guide.ReplaceStructure(
            [
                Section(Guid.NewGuid(), "First", 0, Block(shared, 0, "One.")),
                Section(Guid.NewGuid(), "Second", 1, Block(shared, 0, "Two."))
            ],
            baseRevision: null);

        replaced.IsFailure.Should().BeTrue();
        replaced.Error.Should().Be(GuideErrors.BlockInvalid);
    }

    [Fact]
    public void A_section_needs_a_title()
    {
        var guide = GuideLifecycleTests.Draft();

        guide.ReplaceStructure([Section(Guid.NewGuid(), null, 0)], baseRevision: null)
            .Error.Should().Be(GuideErrors.SectionInvalid);

        guide.ReplaceStructure([Section(Guid.NewGuid(), "   ", 0)], baseRevision: null)
            .Error.Should().Be(GuideErrors.SectionInvalid);
    }

    [Fact]
    public void A_section_title_past_the_ceiling_is_refused()
    {
        var guide = GuideLifecycleTests.Draft();

        guide.ReplaceStructure(
                [Section(Guid.NewGuid(), new string('t', GuideLimits.MaxSectionTitle + 1), 0)],
                baseRevision: null)
            .Error.Should().Be(GuideErrors.SectionInvalid);

        guide.ReplaceStructure(
                [Section(Guid.NewGuid(), new string('t', GuideLimits.MaxSectionTitle), 0)],
                baseRevision: null)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void An_empty_identifier_is_not_an_identifier()
    {
        var guide = GuideLifecycleTests.Draft();

        guide.ReplaceStructure([Section(Guid.Empty, "Titled", 0)], baseRevision: null)
            .Error.Should().Be(GuideErrors.SectionInvalid);

        guide.ReplaceStructure(
                [Section(Guid.NewGuid(), "Titled", 0, Block(Guid.Empty, 0, "Prose."))],
                baseRevision: null)
            .Error.Should().Be(GuideErrors.BlockInvalid);
    }

    [Fact]
    public void Exactly_the_maximum_number_of_blocks_in_one_section_is_allowed()
    {
        var guide = GuideLifecycleTests.Draft();

        guide.ReplaceStructure(
                [Section(Guid.NewGuid(), "Long", 0, Blocks(GuideLimits.MaxBlocksPerSection))],
                baseRevision: null)
            .IsSuccess.Should().BeTrue();

        guide.Sections[0].Blocks.Should().HaveCount(GuideLimits.MaxBlocksPerSection);
    }

    [Fact]
    public void One_block_past_the_section_ceiling_is_refused()
    {
        var guide = GuideLifecycleTests.Draft();

        var replaced = guide.ReplaceStructure(
            [Section(Guid.NewGuid(), "Long", 0, Blocks(GuideLimits.MaxBlocksPerSection + 1))],
            baseRevision: null);

        replaced.IsFailure.Should().BeTrue();
        replaced.Error.Should().Be(GuideErrors.BlockLimit);
    }

    [Fact]
    public void A_block_longer_than_the_ceiling_is_refused()
    {
        var guide = GuideLifecycleTests.Draft();

        var replaced = guide.ReplaceStructure(
            [
                Section(
                    Guid.NewGuid(),
                    "Long",
                    0,
                    Block(Guid.NewGuid(), 0, new string('c', GuideLimits.MaxBlockLength + 1)))
            ],
            baseRevision: null);

        replaced.IsFailure.Should().BeTrue();
        replaced.Error.Should().Be(GuideErrors.BlockInvalid);
    }

    [Theory]
    [InlineData(GuideBlockKind.Image)]
    [InlineData(GuideBlockKind.Video)]
    [InlineData(GuideBlockKind.Attachment)]
    public void A_media_block_without_a_media_reference_is_refused(GuideBlockKind kind)
    {
        // A media block with no media is a broken image on every reader's screen; catching it
        // at save keeps the failure on the author's screen instead.
        var guide = GuideLifecycleTests.Draft();

        var replaced = guide.ReplaceStructure(
            [
                Section(Guid.NewGuid(), "Illustrated", 0, new GuideBlockDraft
                {
                    BlockId = Guid.NewGuid(),
                    Kind = kind,
                    Order = 0,
                    Content = "A caption."
                })
            ],
            baseRevision: null);

        replaced.IsFailure.Should().BeTrue();
        replaced.Error.Should().Be(GuideErrors.BlockInvalid);
    }

    [Fact]
    public void A_media_block_with_a_reference_is_accepted_and_stores_no_url()
    {
        var mediaId = Guid.NewGuid();
        var guide = GuideLifecycleTests.Draft();

        guide.ReplaceStructure(
            [
                Section(Guid.NewGuid(), "Illustrated", 0, new GuideBlockDraft
                {
                    BlockId = Guid.NewGuid(),
                    Kind = GuideBlockKind.Image,
                    Order = 0,
                    Content = "The branch counter.",
                    MediaId = mediaId
                })
            ],
            baseRevision: null).IsSuccess.Should().BeTrue();

        var block = guide.Sections[0].Blocks[0];

        block.MediaId.Should().Be(mediaId, "a reference outlives storage migrations; a URL does not");
        block.IsMediaKind.Should().BeTrue();
    }

    // ── Ordering ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sections_and_blocks_come_back_in_reading_order_whatever_order_they_arrived_in()
    {
        var guide = GuideLifecycleTests.Draft();

        guide.ReplaceStructure(
            [
                Section(Guid.NewGuid(), "Third", 2),
                Section(
                    Guid.NewGuid(),
                    "First",
                    0,
                    Block(Guid.NewGuid(), 1, "Second sentence."),
                    Block(Guid.NewGuid(), 0, "First sentence.")),
                Section(Guid.NewGuid(), "Second", 1)
            ],
            baseRevision: null).IsSuccess.Should().BeTrue();

        guide.Sections.Select(section => section.Title)
            .Should().Equal(
                new[] { "First", "Second", "Third" },
                "the client renders what the aggregate hands back, in that order");

        guide.Sections[0].Blocks.Select(block => block.Content)
            .Should().Equal(new[] { "First sentence.", "Second sentence." });
    }

    // ── Wholesale replacement ───────────────────────────────────────────────────────────────

    [Fact]
    public void Replacing_the_structure_replaces_it_wholesale()
    {
        var guide = GuideLifecycleTests.Draft();

        guide.ReplaceStructure(Sections(5), baseRevision: null).IsSuccess.Should().BeTrue();
        guide.Sections.Should().HaveCount(5);

        guide.ReplaceStructure(
            GuideLifecycleTests.OneSection("Only section"), baseRevision: null)
            .IsSuccess.Should().BeTrue();

        guide.Sections.Should().ContainSingle().Which.Title.Should().Be("Only section",
            "a document save is the whole document, not a merge");
    }

    [Fact]
    public void A_rejected_save_leaves_the_previous_structure_exactly_as_it_was()
    {
        var guide = GuideLifecycleTests.Draft();

        guide.ReplaceStructure(
            GuideLifecycleTests.OneSection("Kept", "The advice that was already there."),
            baseRevision: null).IsSuccess.Should().BeTrue();

        var revision = guide.Revision;

        // The last section is invalid; the whole save must fail, not the last section.
        var replaced = guide.ReplaceStructure(
            [
                Section(Guid.NewGuid(), "New first", 0, Block(Guid.NewGuid(), 0, "Fine.")),
                Section(Guid.NewGuid(), null, 1)
            ],
            baseRevision: null);

        replaced.IsFailure.Should().BeTrue();

        guide.Sections.Should().ContainSingle().Which.Title.Should().Be("Kept",
            "a partially applied document save is a corruption with a success code");

        guide.Revision.Should().Be(revision, "a failed save is not an edit");
    }

    [Fact]
    public void Replacing_the_structure_announces_the_new_shape()
    {
        var guide = GuideLifecycleTests.Draft();
        guide.ClearDomainEvents();

        guide.ReplaceStructure(
            [
                Section(Guid.NewGuid(), "First", 0, Blocks(3)),
                Section(Guid.NewGuid(), "Second", 1, Blocks(2))
            ],
            baseRevision: null).IsSuccess.Should().BeTrue();

        var updated = guide.DomainEvents.OfType<GuideContentUpdatedDomainEvent>().Single();

        updated.SectionCount.Should().Be(2);
        updated.BlockCount.Should().Be(5);
        updated.ChangedBy.Should().BeNull();
    }

    // ── Tags, the other set replaced wholesale ──────────────────────────────────────────────

    [Fact]
    public void Tags_are_normalised_deduplicated_and_kept_in_the_order_the_author_chose()
    {
        // "Bank Account", "bank-account" and "bank account" matching three different chip pages
        // is a discovery surface nobody can curate.
        var guide = GuideLifecycleTests.Draft();

        guide.SetTags(["Bank Account", "  NHS  ", "bank-account", "Right to Rent"], baseRevision: null)
            .IsSuccess.Should().BeTrue();

        guide.Tags.Select(tag => tag.Slug)
            .Should().Equal(new[] { "bank-account", "nhs", "right-to-rent" });

        guide.Tags.Select(tag => tag.Order).Should().Equal(new[] { 0, 1, 2 },
            "the chip editor renders them back in the order they were saved");
    }

    [Fact]
    public void A_tag_that_normalises_to_nothing_fails_the_whole_save()
    {
        var guide = GuideLifecycleTests.Draft();

        guide.SetTags(["housing"], baseRevision: null).IsSuccess.Should().BeTrue();

        guide.SetTags(["banking", "!!!"], baseRevision: null)
            .Error.Should().Be(GuideErrors.TagInvalid);

        guide.Tags.Should().ContainSingle().Which.Slug.Should().Be("housing",
            "a rejected tag save leaves the previous set alone");
    }

    [Fact]
    public void More_tags_than_a_guide_can_carry_are_refused()
    {
        var guide = GuideLifecycleTests.Draft();

        var tooMany = Enumerable.Range(0, GuideLimits.MaxTags + 1)
            .Select(index => $"tag-{index}")
            .ToArray();

        guide.SetTags(tooMany, baseRevision: null).Error.Should().Be(GuideErrors.TagLimit);

        guide.SetTags(tooMany[..GuideLimits.MaxTags], baseRevision: null)
            .IsSuccess.Should().BeTrue();

        guide.Tags.Should().HaveCount(GuideLimits.MaxTags);
    }

    [Fact]
    public void Setting_tags_replaces_the_whole_set()
    {
        var guide = GuideLifecycleTests.Draft();

        guide.SetTags(["banking", "nhs"], baseRevision: null).IsSuccess.Should().BeTrue();
        guide.SetTags(["housing"], baseRevision: null).IsSuccess.Should().BeTrue();

        guide.Tags.Should().ContainSingle().Which.Slug.Should().Be("housing");
    }

    // ── Word counting ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Only_prose_a_reader_reads_is_counted_towards_the_reading_time()
    {
        // Counting a data table's JSON as reading material inflates the reading time of
        // exactly the guides that are quickest to use.
        var guide = GuideLifecycleTests.Draft();

        guide.ReplaceStructure(
            [
                Section(
                    Guid.NewGuid(),
                    "Mixed",
                    0,
                    Block(Guid.NewGuid(), 0, "one two three four five"),
                    new GuideBlockDraft
                    {
                        BlockId = Guid.NewGuid(),
                        Kind = GuideBlockKind.Table,
                        Order = 1,
                        Content = "a b c d e f g h i j",
                        Metadata = "{\"rows\":[]}"
                    },
                    new GuideBlockDraft
                    {
                        BlockId = Guid.NewGuid(),
                        Kind = GuideBlockKind.CodeSnippet,
                        Order = 2,
                        Content = "var x = 1; var y = 2;"
                    },
                    new GuideBlockDraft
                    {
                        BlockId = Guid.NewGuid(),
                        Kind = GuideBlockKind.Warning,
                        Order = 3,
                        Content = "six seven"
                    })
            ],
            baseRevision: null).IsSuccess.Should().BeTrue();

        guide.WordCount.Should().Be(7, "markdown and callouts count; tables and code do not");
    }

    [Fact]
    public void The_calculator_and_the_aggregate_agree_on_the_reading_time()
    {
        // Comparability across guides dies the moment two call sites can disagree about the
        // divisor, so the preview path and the publish path are pinned to each other.
        var guide = GuideLifecycleTests.Drafted(GuideLifecycleTests.Words(1_234));

        var previewed = GuideReadingTimeCalculator.CalculateMinutes(guide);

        guide.Publish(GuideLifecycleTests.Now).IsSuccess.Should().BeTrue();

        guide.Reading.ReadingTimeMinutes.Should().Be(previewed);
    }

    // ── At the ceiling ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A guide built at both ceilings — one hundred sections of two hundred blocks — published,
    /// snapshotted and read back.
    /// <para>
    /// The size is the point: every invariant in this file is easy to hold for a three-section
    /// guide, and the ones that break at twenty thousand blocks break silently. What is asserted
    /// is correctness — the structure survives, the word count and reading time are exact, and
    /// the snapshot round-trips to a byte-identical document. The timings are printed for the
    /// performance report and asserted on nowhere.
    /// </para>
    /// </summary>
    [Fact]
    public void A_guide_at_the_section_and_block_ceilings_publishes_and_round_trips_intact()
    {
        const int WordsPerBlock = 5;

        var sectionCount = GuideLimits.MaxSections;
        var blockCount = GuideLimits.MaxBlocksPerSection;
        var totalBlocks = sectionCount * blockCount;

        var build = Stopwatch.StartNew();

        var drafts = Enumerable.Range(0, sectionCount)
            .Select(sectionIndex => new GuideSectionDraft
            {
                SectionId = Guid.NewGuid(),
                Title = $"Section {sectionIndex}",
                Order = sectionIndex,
                Blocks = Enumerable.Range(0, blockCount)
                    .Select(blockIndex => new GuideBlockDraft
                    {
                        BlockId = Guid.NewGuid(),
                        Kind = GuideBlockKind.Markdown,
                        Order = blockIndex,
                        Content = string.Join(' ', Enumerable.Repeat("word", WordsPerBlock))
                    })
                    .ToList()
            })
            .ToList();

        build.Stop();

        var guide = GuideLifecycleTests.Draft();

        var save = Stopwatch.StartNew();
        var replaced = guide.ReplaceStructure(drafts, baseRevision: null);
        save.Stop();

        replaced.IsSuccess.Should().BeTrue("both ceilings are inclusive bounds");

        guide.Sections.Should().HaveCount(sectionCount);
        guide.Sections.Sum(section => section.Blocks.Count).Should().Be(totalBlocks);
        guide.WordCount.Should().Be(totalBlocks * WordsPerBlock);

        var publish = Stopwatch.StartNew();
        guide.Publish(GuideLifecycleTests.Now).IsSuccess.Should().BeTrue();
        publish.Stop();

        guide.Reading.ReadingTimeMinutes.Should()
            .Be(totalBlocks * WordsPerBlock / GuideLimits.WordsPerMinute);

        var serialize = Stopwatch.StartNew();
        var snapshot = GuideSnapshotSerializer.Serialize(guide);
        serialize.Stop();

        var roundTrip = Stopwatch.StartNew();
        var read = GuideSnapshotSerializer.Deserialize(snapshot);
        read.IsSuccess.Should().BeTrue("a snapshot this module wrote must be one it can read");

        var restoredDrafts = GuideSnapshotSerializer.ToDrafts(read.Value);
        restoredDrafts.IsSuccess.Should().BeTrue();
        roundTrip.Stop();

        var restored = GuideLifecycleTests.Draft();
        restored.ReplaceStructure(restoredDrafts.Value, baseRevision: null)
            .IsSuccess.Should().BeTrue();

        restored.Sections.Should().HaveCount(sectionCount);
        restored.WordCount.Should().Be(guide.WordCount);

        GuideSnapshotSerializer.Serialize(restored).Should().Be(snapshot,
            "a version that cannot be restored to the document it recorded is not a record of it");

        _output.WriteLine(
            $"{sectionCount} sections x {blockCount} blocks = {totalBlocks:N0} blocks, " +
            $"{guide.WordCount:N0} words, {snapshot.Length:N0} bytes of JSON. " +
            $"built {build.ElapsedMilliseconds:N0}ms, saved {save.ElapsedMilliseconds:N0}ms, " +
            $"published {publish.ElapsedMilliseconds:N0}ms, " +
            $"serialized {serialize.ElapsedMilliseconds:N0}ms, " +
            $"round-tripped {roundTrip.ElapsedMilliseconds:N0}ms.");
    }

    // ── Builders ────────────────────────────────────────────────────────────────────────────

    private static List<GuideSectionDraft> Sections(int count) =>
        Enumerable.Range(0, count)
            .Select(index => Section(
                Guid.NewGuid(),
                $"Section {index}",
                index,
                Block(Guid.NewGuid(), 0, $"Content of section {index}.")))
            .ToList();

    private static GuideSectionDraft Section(
        Guid sectionId, string? title, int order, params GuideBlockDraft[] blocks) =>
        new()
        {
            SectionId = sectionId,
            Title = title,
            Order = order,
            Blocks = blocks
        };

    private static GuideBlockDraft Block(Guid blockId, int order, string content) =>
        new()
        {
            BlockId = blockId,
            Kind = GuideBlockKind.Markdown,
            Order = order,
            Content = content
        };

    private static GuideBlockDraft[] Blocks(int count) =>
        Enumerable.Range(0, count)
            .Select(index => Block(Guid.NewGuid(), index, $"Block {index}."))
            .ToArray();
}
