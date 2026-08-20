using Diaspora.Modules.Community.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Community;

/// <summary>Cursors have to round-trip, and a bad one has to be survivable.</summary>
public class FeedCursorTests
{
    [Fact]
    public void A_cursor_round_trips_through_its_encoding()
    {
        var id = Guid.NewGuid();
        var cursor = FeedCursor.Create(1_722_000_000_000d, id);

        var decoded = FeedCursor.Decode(cursor.Encode());

        decoded.Should().NotBeNull();
        decoded!.SortKey.Should().Be(cursor.SortKey);
        decoded.LastId.Should().Be(id);
    }

    [Fact]
    public void A_timestamp_cursor_recovers_its_instant_to_the_millisecond()
    {
        var when = new DateTimeOffset(2026, 7, 28, 9, 30, 15, 250, TimeSpan.Zero);

        var decoded = FeedCursor.Decode(FeedCursor.FromTimestamp(when, Guid.NewGuid()).Encode());

        decoded!.AsTimestamp().Should().Be(when);
    }

    [Fact]
    public void A_negative_sort_key_survives_the_round_trip()
    {
        // The proximity ranking negates distance so nearer sorts higher. A cursor that lost the
        // sign would return the second page in the opposite order from the first.
        var decoded = FeedCursor.Decode(FeedCursor.Create(-12.5d, Guid.NewGuid()).Encode());

        decoded!.SortKey.Should().Be(-12.5d);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-base64-!!")]
    [InlineData("YWJjZGVm")]
    public void An_unreadable_cursor_decodes_to_null_rather_than_throwing(string? raw)
    {
        // A malformed cursor is a stale client or a truncated URL. The right response is the
        // first page, not a 400 that leaves an infinite scroll permanently stuck.
        FeedCursor.Decode(raw).Should().BeNull();
    }

    [Fact]
    public void The_encoding_is_url_safe()
    {
        var encoded = FeedCursor.Create(double.MaxValue, Guid.NewGuid()).Encode();

        encoded.Should().NotContain("+").And.NotContain("/").And.NotContain("=");
    }
}
