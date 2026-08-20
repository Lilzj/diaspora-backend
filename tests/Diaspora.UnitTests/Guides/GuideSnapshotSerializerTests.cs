using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Application.Services.Snapshots;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Guides;

/// <summary>
/// The one place a guide's structure becomes JSON and comes back.
/// <para>
/// <b>Byte-stability is the requirement, not a nicety.</b> Version snapshots are compared,
/// archived and diffed by operators in a database console; two serializations of identical
/// content that differ by property order make every one of those useless. B1.2 records what
/// happens when the order is left to reflection — it is an accident that changes out from under
/// you at the next runtime — so the order is pinned by attribute and asserted here.
/// </para>
/// <para>
/// The other half is that a corrupt row degrades to an error result rather than a 500: a guide
/// whose version cannot be read must still render, and its history must still list.
/// </para>
/// </summary>
public class GuideSnapshotSerializerTests
{
    [Fact]
    public void Serializing_the_same_structure_twice_produces_identical_bytes()
    {
        var guide = GuideLifecycleTests.Draft();

        guide.ReplaceStructure(Structure(), baseRevision: null).IsSuccess.Should().BeTrue();

        var first = GuideSnapshotSerializer.Serialize(guide);
        var second = GuideSnapshotSerializer.Serialize(guide);

        second.Should().Be(first, "an operator diffing two versions must see only real changes");
    }

    [Fact]
    public void Two_guides_with_identical_structure_serialize_identically()
    {
        var structure = Structure();

        var first = GuideLifecycleTests.Draft();
        var second = GuideLifecycleTests.Draft();

        first.ReplaceStructure(structure, baseRevision: null).IsSuccess.Should().BeTrue();
        second.ReplaceStructure(structure, baseRevision: null).IsSuccess.Should().BeTrue();

        GuideSnapshotSerializer.Serialize(second)
            .Should().Be(GuideSnapshotSerializer.Serialize(first),
                "the snapshot is a function of the content and of nothing else");
    }

    [Fact]
    public void The_property_order_is_pinned_rather_than_left_to_reflection()
    {
        var guide = GuideLifecycleTests.Draft();

        guide.ReplaceStructure(Structure(), baseRevision: null).IsSuccess.Should().BeTrue();

        var json = GuideSnapshotSerializer.Serialize(guide);

        json.Should().StartWith("{\"schemaVersion\":1,\"sections\":[",
            "the schema version is first so a future reader can know the shape before committing to it");

        IndexOfKey(json, "sectionId").Should().BeLessThan(IndexOfKey(json, "title"));
        IndexOfKey(json, "title").Should().BeLessThan(IndexOfKey(json, "order"));
        IndexOfKey(json, "order").Should().BeLessThan(IndexOfKey(json, "blocks"));
        IndexOfKey(json, "blockId").Should().BeLessThan(IndexOfKey(json, "kind"));
        IndexOfKey(json, "kind").Should().BeLessThan(IndexOfKey(json, "content"));
        IndexOfKey(json, "content").Should().BeLessThan(IndexOfKey(json, "mediaId"));
        IndexOfKey(json, "mediaId").Should().BeLessThan(IndexOfKey(json, "metadata"));
    }

    [Fact]
    public void Nothing_that_was_not_written_appears_in_the_document()
    {
        var guide = GuideLifecycleTests.Drafted();

        var json = GuideSnapshotSerializer.Serialize(guide);

        json.Should().NotContain("null", "an absent field costs bytes on every version row");
        json.Should().NotContain("\n", "the stored form is compact, not pretty-printed");
    }

    [Fact]
    public void A_block_kind_is_stored_as_text_so_a_snapshot_outlives_the_enum()
    {
        var guide = GuideLifecycleTests.Draft();

        guide.ReplaceStructure(Structure(), baseRevision: null).IsSuccess.Should().BeTrue();

        GuideSnapshotSerializer.Serialize(guide).Should().Contain("\"kind\":\"Markdown\"",
            "a snapshot written today must deserialize in five years, whatever the numbering has become");
    }

    // ── Round trip ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_snapshot_round_trips_to_the_document_it_recorded()
    {
        var mediaId = Guid.NewGuid();
        var structure = Structure(mediaId);

        var original = GuideLifecycleTests.Draft();
        original.ReplaceStructure(structure, baseRevision: null).IsSuccess.Should().BeTrue();

        var json = GuideSnapshotSerializer.Serialize(original);

        var read = GuideSnapshotSerializer.Deserialize(json);
        read.IsSuccess.Should().BeTrue();

        var drafts = GuideSnapshotSerializer.ToDrafts(read.Value);
        drafts.IsSuccess.Should().BeTrue();

        var restored = GuideLifecycleTests.Draft();
        restored.ReplaceStructure(drafts.Value, baseRevision: null).IsSuccess.Should().BeTrue();

        restored.Sections.Should().HaveCount(2);
        restored.Sections[0].Title.Should().Be("Before you start");
        restored.Sections[1].Blocks.Should().HaveCount(3);
        restored.Sections[1].Blocks[1].MediaId.Should().Be(mediaId);
        restored.Sections[1].Blocks[1].Kind.Should().Be(GuideBlockKind.Image);

        GuideSnapshotSerializer.Serialize(restored).Should().Be(json,
            "a version that does not round-trip is not a record of what a reader was shown");
    }

    [Fact]
    public void The_opaque_table_payload_survives_verbatim()
    {
        const string Payload = "{\"columns\":[\"Fee\",\"Amount\"],\"rows\":[[\"Opening\",\"£0\"]]}";

        var guide = GuideLifecycleTests.Draft();

        guide.ReplaceStructure(
            [
                new GuideSectionDraft
                {
                    SectionId = Guid.NewGuid(),
                    Title = "Fees",
                    Order = 0,
                    Blocks =
                    [
                        new GuideBlockDraft
                        {
                            BlockId = Guid.NewGuid(),
                            Kind = GuideBlockKind.Table,
                            Order = 0,
                            Metadata = Payload
                        }
                    ]
                }
            ],
            baseRevision: null).IsSuccess.Should().BeTrue();

        var read = GuideSnapshotSerializer.Deserialize(GuideSnapshotSerializer.Serialize(guide));

        read.Value.Sections[0].Blocks[0].Metadata.Should().Be(Payload,
            "the editor owns the shape and the domain deliberately does not parse it");
    }

    // ── Failure is a result, never an exception ─────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{\"sections\":")]
    public void A_corrupt_row_degrades_to_an_error_rather_than_a_500(string? json)
    {
        var read = GuideSnapshotSerializer.Deserialize(json);

        read.IsFailure.Should().BeTrue();
        read.Error.Should().Be(GuideErrors.SnapshotInvalid);
    }

    [Fact]
    public void An_unknown_block_kind_fails_the_whole_restore_rather_than_dropping_the_block()
    {
        // A partial restore is a corruption with a success code: the author sees their guide
        // back, minus a paragraph nobody told them about.
        var snapshot = new GuideSnapshot
        {
            Sections =
            [
                new GuideSectionSnapshot
                {
                    SectionId = Guid.NewGuid(),
                    Title = "Before you start",
                    Order = 0,
                    Blocks =
                    [
                        new GuideBlockSnapshot
                        {
                            BlockId = Guid.NewGuid(),
                            Kind = "Markdown",
                            Order = 0,
                            Content = "Bring your passport."
                        },
                        new GuideBlockSnapshot
                        {
                            BlockId = Guid.NewGuid(),
                            Kind = "HolographicProjection",
                            Order = 1
                        }
                    ]
                }
            ]
        };

        var drafts = GuideSnapshotSerializer.ToDrafts(snapshot);

        drafts.IsFailure.Should().BeTrue();
        drafts.Error.Should().Be(GuideErrors.SnapshotInvalid);
    }

    [Fact]
    public void A_block_kind_is_read_back_case_insensitively()
    {
        // Strictness on the way in would make the store brittle against its own history.
        var snapshot = new GuideSnapshot
        {
            Sections =
            [
                new GuideSectionSnapshot
                {
                    SectionId = Guid.NewGuid(),
                    Title = "Before you start",
                    Order = 0,
                    Blocks =
                    [
                        new GuideBlockSnapshot
                        {
                            BlockId = Guid.NewGuid(),
                            Kind = "markdown",
                            Order = 0,
                            Content = "Bring your passport."
                        }
                    ]
                }
            ]
        };

        var drafts = GuideSnapshotSerializer.ToDrafts(snapshot);

        drafts.IsSuccess.Should().BeTrue();
        drafts.Value[0].Blocks[0].Kind.Should().Be(GuideBlockKind.Markdown);
    }

    [Fact]
    public void An_already_built_snapshot_serializes_through_the_same_fixed_options()
    {
        var snapshot = new GuideSnapshot
        {
            Sections =
            [
                new GuideSectionSnapshot
                {
                    SectionId = Guid.Parse("00000000-0000-0000-0000-0000000000f1"),
                    Title = "Before you start",
                    Order = 0
                }
            ]
        };

        var json = GuideSnapshotSerializer.Serialize(snapshot);

        json.Should().Be(
            "{\"schemaVersion\":1,\"sections\":[{\"sectionId\":\"00000000-0000-0000-0000-0000000000f1\"," +
            "\"title\":\"Before you start\",\"order\":0,\"blocks\":[]}]}",
            "the auto-save path and the publish path must write the same shape");
    }

    private static int IndexOfKey(string json, string key) =>
        json.IndexOf($"\"{key}\":", StringComparison.Ordinal);

    private static List<GuideSectionDraft> Structure(Guid? mediaId = null) =>
        [
            new GuideSectionDraft
            {
                SectionId = Guid.Parse("00000000-0000-0000-0000-0000000000a1"),
                Title = "Before you start",
                Order = 0,
                Blocks =
                [
                    new GuideBlockDraft
                    {
                        BlockId = Guid.Parse("00000000-0000-0000-0000-0000000000b1"),
                        Kind = GuideBlockKind.Markdown,
                        Order = 0,
                        Content = "Bring your passport and a proof of address."
                    }
                ]
            },
            new GuideSectionDraft
            {
                SectionId = Guid.Parse("00000000-0000-0000-0000-0000000000a2"),
                Title = "At the branch",
                Order = 1,
                Blocks =
                [
                    new GuideBlockDraft
                    {
                        BlockId = Guid.Parse("00000000-0000-0000-0000-0000000000b2"),
                        Kind = GuideBlockKind.Markdown,
                        Order = 0,
                        Content = "Ask for a basic account."
                    },
                    new GuideBlockDraft
                    {
                        BlockId = Guid.Parse("00000000-0000-0000-0000-0000000000b3"),
                        Kind = GuideBlockKind.Image,
                        Order = 1,
                        Content = "The counter.",
                        MediaId = mediaId ?? Guid.Parse("00000000-0000-0000-0000-0000000000c1")
                    },
                    new GuideBlockDraft
                    {
                        BlockId = Guid.Parse("00000000-0000-0000-0000-0000000000b4"),
                        Kind = GuideBlockKind.Table,
                        Order = 2,
                        Metadata = "{\"columns\":[\"Fee\"],\"rows\":[]}"
                    }
                ]
            }
        ];
}
