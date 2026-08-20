using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Media;

/// <summary>
/// <c>MediaOwnerType</c>'s compatibility contract, and PSR-1's addition to it.
/// <para>
/// <b>The enum is a wire vocabulary, and adding to it is the one change B5 expected a later phase
/// to make.</b> Owner types are stored as text — <c>owner_type</c> carries <c>"Guide"</c>, not
/// <c>5</c> — and parsed case-insensitively from the name, so a new member is additive on the wire
/// and in the database. What would not be additive is renaming one, renumbering one, or removing
/// one, and the rules below are what make that visible.
/// </para>
/// <para>
/// PSR-1 added <see cref="MediaOwnerType.Comment"/> to close DEBT-008. Its absence was a live
/// defect rather than a gap: B6's comment has carried an attachment since it shipped and had no
/// owner type to claim it under, so the orphan sweeper collected every comment image a day after
/// it was posted.
/// </para>
/// </summary>
public class MediaOwnerTypeCompatibilityTests
{
    /// <summary>
    /// <b>Every value that has ever shipped keeps its number.</b> The column is text today, so a
    /// renumbering would not corrupt stored data — but the numbers are what an index, a log line
    /// and any future numeric projection would carry, and the enum's own summary promises they are
    /// stable. Pinned literally rather than by count, because a count catches an addition and this
    /// is about the fifteen that were already there.
    /// </summary>
    [Theory]
    [InlineData(MediaOwnerType.Unassigned, 0)]
    [InlineData(MediaOwnerType.UserProfile, 1)]
    [InlineData(MediaOwnerType.Verification, 2)]
    [InlineData(MediaOwnerType.Community, 3)]
    [InlineData(MediaOwnerType.Provider, 4)]
    [InlineData(MediaOwnerType.Guide, 5)]
    [InlineData(MediaOwnerType.CommunityUpdate, 6)]
    [InlineData(MediaOwnerType.Job, 7)]
    [InlineData(MediaOwnerType.HousingListing, 8)]
    [InlineData(MediaOwnerType.MarketplaceListing, 9)]
    [InlineData(MediaOwnerType.Event, 10)]
    [InlineData(MediaOwnerType.Organization, 11)]
    [InlineData(MediaOwnerType.Message, 12)]
    [InlineData(MediaOwnerType.Location, 13)]
    [InlineData(MediaOwnerType.AiRequest, 14)]
    [InlineData(MediaOwnerType.Other, 99)]
    public void Every_owner_type_that_shipped_before_psr1_keeps_its_value(
        MediaOwnerType type, int expected)
    {
        ((int)type).Should().Be(
            expected,
            "renumbering {0} would change what every index, log line and numeric projection " +
            "means for rows written before the change",
            type);
    }

    /// <summary>
    /// PSR-1's addition, at the end and not in the middle. Inserting it would have shifted every
    /// value above it.
    /// </summary>
    [Fact]
    public void The_comment_owner_type_is_appended_after_the_values_that_preceded_it()
    {
        ((int)MediaOwnerType.Comment).Should().Be(15);

        ((int)MediaOwnerType.Comment).Should().BeGreaterThan(
            (int)MediaOwnerType.AiRequest,
            "an owner type added in the middle silently renumbers the ones after it");

        ((int)MediaOwnerType.Comment).Should().BeLessThan(
            (int)MediaOwnerType.Other,
            "Other is the reserved escape hatch and stays last");
    }

    /// <summary>
    /// <b>The claim path is a string, and this is the string.</b> Community claims a comment's
    /// attachment by sending <c>nameof(MediaOwnerType.Comment)</c> through Media's frozen request
    /// contract; if the name did not round-trip through the wire mapping, the claim would be
    /// refused and the asset would stay in the orphan population — which is the exact defect this
    /// member was added to fix.
    /// </summary>
    [Fact]
    public void The_comment_owner_type_round_trips_through_the_wire_mapping()
    {
        MediaWireNames.ToWire(MediaOwnerType.Comment).Should().Be("comment");

        MediaWireNames.ParseOrDefault(nameof(MediaOwnerType.Comment), MediaOwnerType.Other)
            .Should().Be(
                MediaOwnerType.Comment,
                "Community sends the enum's own name, which is what the claimer writes");

        MediaWireNames.ParseOrDefault("comment", MediaOwnerType.Other)
            .Should().Be(MediaOwnerType.Comment, "clients differ in how they capitalise");
    }

    /// <summary>
    /// A comment's attachment is its own owner, not the post's.
    /// <para>
    /// Reusing <see cref="MediaOwnerType.CommunityUpdate"/> would have avoided this whole change
    /// and been wrong: the two are swept, counted and access-checked as one owner, so deleting a
    /// comment would have taken the post's images with it.
    /// </para>
    /// </summary>
    [Fact]
    public void A_comment_is_a_distinct_owner_from_the_post_it_sits_under()
    {
        MediaOwnerType.Comment.Should().NotBe(MediaOwnerType.CommunityUpdate);
        MediaOwnerType.Comment.Should().NotBe(MediaOwnerType.Community);
    }

    /// <summary>
    /// An unknown owner type must not silently become a real one. The parse is case-insensitive
    /// by design and forgiving of nothing else.
    /// </summary>
    [Fact]
    public void An_unrecognised_owner_type_falls_back_rather_than_guessing()
    {
        MediaWireNames.ParseOrDefault("kommentar", MediaOwnerType.Other)
            .Should().Be(MediaOwnerType.Other);
    }
}
