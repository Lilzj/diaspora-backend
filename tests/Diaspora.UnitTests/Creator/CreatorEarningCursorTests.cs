using Diaspora.Modules.Creator.Domain.Earnings;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Creator;

/// <summary>
/// The page marker for a creator's ledger.
/// <para>
/// <b>Worth its own file because every failure here loses a row of somebody's income silently.</b>
/// A cursor that does not round-trip pages past an earning; a cursor that parses something the
/// platform did not issue lets a client decide where a page boundary falls; a cursor that is only a
/// timestamp drops whichever of two same-instant earnings the boundary sits between. None of those
/// throws, and none of them is visible on the screen — the ledger simply has a hole in it.
/// </para>
/// </summary>
public sealed class CreatorEarningCursorTests
{
    private static readonly DateTimeOffset Instant =
        new(2026, 9, 1, 10, 0, 0, 123, TimeSpan.FromHours(1));

    private static readonly Guid Row = Guid.Parse("a3f0e2c1-0000-4000-8000-000000000001");

    [Fact]
    public void It_round_trips_exactly()
    {
        var text = new CreatorEarningCursor(Instant, Row).ToString();

        CreatorEarningCursor.TryParse(text, out var parsed).Should().BeTrue();

        // Ticks and offset both, not just the second. A cursor rounded to the second sits *after*
        // the row it names when that row landed mid-second, and the next page starts one earning
        // late — for ever, because every subsequent cursor inherits the skip.
        parsed.OccurredAt.Should().Be(Instant);
        parsed.OccurredAt.Offset.Should().Be(Instant.Offset);
        parsed.Id.Should().Be(Row);
    }

    [Fact]
    public void It_carries_the_row_as_well_as_the_instant()
    {
        // The whole reason it is not a DateTimeOffset. One purchase settling against two subjects
        // records both at the same instant, and a timestamp-only cursor cannot tell them apart.
        var first = new CreatorEarningCursor(Instant, Guid.Parse("11111111-0000-4000-8000-000000000001"));
        var second = new CreatorEarningCursor(Instant, Guid.Parse("22222222-0000-4000-8000-000000000002"));

        first.ToString().Should().NotBe(second.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-cursor")]
    [InlineData("2026-09-01T10:00:00Z")]
    [InlineData("2026-09-01T10:00:00Z|not-a-guid")]
    [InlineData("not-a-date|a3f0e2c1000040008000000000000001")]
    [InlineData("2026-09-01T10:00:00Z|a3f0e2c1000040008000000000000001|extra")]
    public void It_refuses_anything_it_did_not_issue(string? candidate)
    {
        // Refused rather than ignored. The handler turns a false here into a validation failure;
        // ignoring it would serve page one again, and a "load more" that reloads the rows already on
        // screen reads to a creator as a ledger that ends where it does not.
        CreatorEarningCursor.TryParse(candidate, out var parsed).Should().BeFalse();

        parsed.Should().Be(default(CreatorEarningCursor));
    }

    [Fact]
    public void A_bare_timestamp_is_not_a_cursor()
    {
        // Stated separately from the theory above because this is the plausible wrong shape — the
        // sibling activity route really does page on a bare DateTimeOffset, and a developer
        // generalising from it would send one here.
        CreatorEarningCursor.TryParse(Instant.ToString("O"), out _).Should().BeFalse();
    }
}
