using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Application.Services.Snapshots;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Guides;

/// <summary>
/// What changed between two published versions, at section granularity.
/// <para>
/// <b>Sections are matched by their stable identifiers and never by position or title.</b> The
/// editor keeps a section's identifier across saves, so a retitle and a replacement are
/// different facts — and a diff that matched by position would report a single inserted
/// paragraph as every section having changed, which is the diff nobody reads twice.
/// </para>
/// <para>
/// Unchanged sections are omitted. The diff exists to show what moved, and a reader comparing
/// two versions of a forty-section guide needs the three that differ, not all forty.
/// </para>
/// </summary>
public class GuideVersionDiffTests
{
    private static readonly Guid GuideIdentifier = Guid.Parse("00000000-0000-0000-0000-0000000000f0");

    private static readonly Guid First = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Second = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid Third = Guid.Parse("00000000-0000-0000-0000-0000000000a3");

    [Fact]
    public void Two_identical_versions_differ_in_nothing()
    {
        var snapshot = Snapshot(Section(First, "Before you start", 0, "Bring your passport."));

        var diff = GuideVersionDiffService.Diff(GuideIdentifier, 1, snapshot, 2, snapshot);

        diff.GuideId.Should().Be(GuideIdentifier);
        diff.FromVersion.Should().Be(1);
        diff.ToVersion.Should().Be(2);
        diff.Sections.Should().BeEmpty("the diff exists to show what moved");
    }

    [Fact]
    public void A_section_present_only_in_the_newer_version_is_added()
    {
        var from = Snapshot(Section(First, "Before you start", 0, "Bring your passport."));

        var to = Snapshot(
            Section(First, "Before you start", 0, "Bring your passport."),
            Section(Second, "At the branch", 1, "Ask for a basic account."));

        var diff = GuideVersionDiffService.Diff(GuideIdentifier, 1, from, 2, to);

        var added = diff.Sections.Should().ContainSingle().Subject;

        added.SectionId.Should().Be(Second);
        added.Change.Should().Be("added");
        added.ToTitle.Should().Be("At the branch");
        added.FromTitle.Should().BeNull();
        added.ContentChanged.Should().BeTrue();
    }

    [Fact]
    public void A_section_present_only_in_the_older_version_is_removed()
    {
        var from = Snapshot(
            Section(First, "Before you start", 0, "Bring your passport."),
            Section(Second, "At the branch", 1, "Ask for a basic account."));

        var to = Snapshot(Section(First, "Before you start", 0, "Bring your passport."));

        var removed = GuideVersionDiffService.Diff(GuideIdentifier, 1, from, 2, to)
            .Sections.Should().ContainSingle().Subject;

        removed.SectionId.Should().Be(Second);
        removed.Change.Should().Be("removed");
        removed.FromTitle.Should().Be("At the branch");
        removed.ToTitle.Should().BeNull();
        removed.ContentChanged.Should().BeTrue();
    }

    [Fact]
    public void A_section_that_kept_its_identifier_and_changed_its_title_is_retitled()
    {
        var from = Snapshot(Section(First, "Before you start", 0, "Bring your passport."));
        var to = Snapshot(Section(First, "What to bring", 0, "Bring your passport."));

        var retitled = GuideVersionDiffService.Diff(GuideIdentifier, 1, from, 2, to)
            .Sections.Should().ContainSingle().Subject;

        retitled.Change.Should().Be("retitled");
        retitled.FromTitle.Should().Be("Before you start");
        retitled.ToTitle.Should().Be("What to bring");
        retitled.ContentChanged.Should().BeFalse(
            "a heading correction must not read as a rewrite of the advice under it");
    }

    [Fact]
    public void A_section_whose_prose_changed_reports_content_changed()
    {
        var from = Snapshot(Section(First, "Before you start", 0, "Bring your passport."));
        var to = Snapshot(Section(First, "Before you start", 0, "Bring your passport and a payslip."));

        var changed = GuideVersionDiffService.Diff(GuideIdentifier, 1, from, 2, to)
            .Sections.Should().ContainSingle().Subject;

        changed.Change.Should().Be("content-changed");
        changed.ContentChanged.Should().BeTrue();
        changed.FromTitle.Should().Be("Before you start");
        changed.ToTitle.Should().Be("Before you start");
    }

    [Fact]
    public void A_section_that_was_both_retitled_and_rewritten_reports_the_content_change()
    {
        // The stronger fact wins: a reader told only "retitled" would not re-read the advice.
        var from = Snapshot(Section(First, "Before you start", 0, "Bring your passport."));
        var to = Snapshot(Section(First, "What to bring", 0, "Bring your passport and a payslip."));

        GuideVersionDiffService.Diff(GuideIdentifier, 1, from, 2, to)
            .Sections.Should().ContainSingle().Subject
            .Change.Should().Be("content-changed");
    }

    [Fact]
    public void Reordering_a_section_without_touching_it_is_not_a_change()
    {
        var from = Snapshot(
            Section(First, "Before you start", 0, "Bring your passport."),
            Section(Second, "At the branch", 1, "Ask for a basic account."));

        var to = Snapshot(
            Section(Second, "At the branch", 0, "Ask for a basic account."),
            Section(First, "Before you start", 1, "Bring your passport."));

        GuideVersionDiffService.Diff(GuideIdentifier, 1, from, 2, to)
            .Sections.Should().BeEmpty(
                "moving a chapter changes no advice, and reporting it as two rewrites hides the ones that are");
    }

    [Fact]
    public void An_added_block_is_a_content_change_even_when_every_existing_block_is_untouched()
    {
        var from = Snapshot(Section(First, "Before you start", 0, "Bring your passport."));

        var to = Snapshot(new GuideSectionSnapshot
        {
            SectionId = First,
            Title = "Before you start",
            Order = 0,
            Blocks =
            [
                Block("Bring your passport."),
                Block("And a proof of address.")
            ]
        });

        GuideVersionDiffService.Diff(GuideIdentifier, 1, from, 2, to)
            .Sections.Should().ContainSingle().Subject
            .ContentChanged.Should().BeTrue();
    }

    [Fact]
    public void Reordering_blocks_inside_a_section_is_a_content_change()
    {
        var one = Block("Bring your passport.");
        var two = Block("And a proof of address.");

        var from = Snapshot(new GuideSectionSnapshot
        {
            SectionId = First,
            Title = "Before you start",
            Order = 0,
            Blocks = [one with { Order = 0 }, two with { Order = 1 }]
        });

        var to = Snapshot(new GuideSectionSnapshot
        {
            SectionId = First,
            Title = "Before you start",
            Order = 0,
            Blocks = [two with { Order = 0 }, one with { Order = 1 }]
        });

        GuideVersionDiffService.Diff(GuideIdentifier, 1, from, 2, to)
            .Sections.Should().ContainSingle().Subject
            .ContentChanged.Should().BeTrue("the order a procedure is performed in is the procedure");
    }

    [Fact]
    public void Swapping_a_media_reference_is_a_content_change_even_with_the_same_caption()
    {
        var from = Snapshot(new GuideSectionSnapshot
        {
            SectionId = First,
            Title = "At the branch",
            Order = 0,
            Blocks = [Block("The counter.") with { Kind = "Image", MediaId = Guid.NewGuid() }]
        });

        var to = Snapshot(new GuideSectionSnapshot
        {
            SectionId = First,
            Title = "At the branch",
            Order = 0,
            Blocks = [from.Sections[0].Blocks[0] with { MediaId = Guid.NewGuid() }]
        });

        GuideVersionDiffService.Diff(GuideIdentifier, 1, from, 2, to)
            .Sections.Should().ContainSingle().Subject
            .ContentChanged.Should().BeTrue();
    }

    [Fact]
    public void Replacing_a_section_outright_reads_as_an_addition_and_a_removal()
    {
        // The stable identifier is what makes this distinguishable from a retitle, and the two
        // mean different things to somebody auditing what a guide told people.
        var from = Snapshot(Section(First, "Before you start", 0, "Bring your passport."));
        var to = Snapshot(Section(Second, "Before you start", 0, "Bring your passport."));

        var diff = GuideVersionDiffService.Diff(GuideIdentifier, 1, from, 2, to);

        diff.Sections.Should().HaveCount(2);
        diff.Sections[0].Change.Should().Be("added");
        diff.Sections[0].SectionId.Should().Be(Second);
        diff.Sections[1].Change.Should().Be("removed");
        diff.Sections[1].SectionId.Should().Be(First);
    }

    [Fact]
    public void Changes_are_listed_in_the_newer_versions_reading_order_and_removals_come_last()
    {
        var from = Snapshot(
            Section(First, "Before you start", 0, "Bring your passport."),
            Section(Second, "At the branch", 1, "Ask for a basic account."),
            Section(Third, "Afterwards", 2, "Wait for the card."));

        var to = Snapshot(
            Section(Third, "Afterwards", 0, "Wait for the card, then set up direct debits."),
            Section(First, "What to bring", 1, "Bring your passport."));

        var diff = GuideVersionDiffService.Diff(GuideIdentifier, 1, from, 2, to);

        diff.Sections.Select(section => section.SectionId)
            .Should().Equal(
                new[] { Third, First, Second },
                "a reader scans the new version top to bottom, then learns what is gone");

        diff.Sections[0].Change.Should().Be("content-changed");
        diff.Sections[1].Change.Should().Be("retitled");
        diff.Sections[2].Change.Should().Be("removed");
    }

    [Fact]
    public void The_diff_is_a_pure_function_of_its_two_inputs()
    {
        // Purity is what makes the endpoint cacheable: the same two versions answer the same
        // way forever.
        var from = Snapshot(Section(First, "Before you start", 0, "Bring your passport."));
        var to = Snapshot(Section(First, "What to bring", 0, "Bring your passport."));

        var once = GuideVersionDiffService.Diff(GuideIdentifier, 1, from, 2, to);
        var twice = GuideVersionDiffService.Diff(GuideIdentifier, 1, from, 2, to);

        twice.Should().BeEquivalentTo(once);
    }

    private static GuideSnapshot Snapshot(params GuideSectionSnapshot[] sections) =>
        new() { Sections = sections };

    private static GuideSectionSnapshot Section(Guid id, string title, int order, string content) =>
        new()
        {
            SectionId = id,
            Title = title,
            Order = order,
            Blocks = [Block(content)]
        };

    private static GuideBlockSnapshot Block(string content) =>
        new()
        {
            BlockId = Guid.Parse("00000000-0000-0000-0000-0000000000b0"),
            Kind = "Markdown",
            Order = 0,
            Content = content
        };
}
