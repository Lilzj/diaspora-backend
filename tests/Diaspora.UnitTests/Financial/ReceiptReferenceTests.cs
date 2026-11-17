using Diaspora.Modules.Financial.Extensibility.Receipts;
using Diaspora.Modules.Financial.Infrastructure.Receipts;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// BBL-004. What a receipt reference has to withstand, and the arithmetic that says it does.
/// <para>
/// <b>The original was six characters from a 32-symbol alphabet and nothing anywhere said how many
/// bits that was.</b> Thirty — on a route that served any caller holding a reference, which put a
/// day's receipts about a million requests away. The number was chosen for legibility, which was a
/// real requirement; what was missing was a second requirement written down beside it, so no review
/// and no test had anything to object to.
/// </para>
/// <para>
/// These tests exist to make the floor fail loudly rather than drift. Two of them carry the
/// arithmetic in the assertion, because a test that asserts a length tells a later reader nothing
/// about why that length.
/// </para>
/// </summary>
public class ReceiptReferenceTests
{
    /// <summary>No I, O, 0 or 1 — the four characters a person reading aloud gets wrong.</summary>
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    private static readonly DateTimeOffset Noon =
        new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    private readonly ReceiptReferenceGenerator _generator = new();

    [Fact]
    public void A_handle_is_short_enough_to_read_over_a_telephone()
    {
        var reference = _generator.Next("DON", Noon, ReceiptReferenceStrength.Handle);

        var parts = reference.Split('-');

        parts.Should().HaveCount(3);
        parts[0].Should().Be("DON");
        parts[1].Should().Be("20261117", "the issue date, so a collision only has to clear one day");
        parts[2].Should().HaveLength(6);
        parts[2].ToCharArray().Should().OnlyContain(
            character => Alphabet.Contains(character, StringComparison.Ordinal));
    }

    /// <summary>
    /// The floor from <see cref="IReceiptReferenceGenerator"/>, asserted with its arithmetic rather
    /// than as a length. A later edit that shortens the suffix to save eight characters on a screen
    /// fails here with the reason in the message.
    /// </summary>
    [Fact]
    public void A_credential_clears_a_hundred_and_twenty_eight_bits()
    {
        var reference = _generator.Next("DON", Noon, ReceiptReferenceStrength.Credential);

        var suffix = reference.Split('-')[2];

        (suffix.Length * ReceiptReferenceGenerator.BitsPerCharacter)
            .Should().BeGreaterThanOrEqualTo(
                128,
                "an ownerless receipt has no owner to compare a caller against, so this string is "
                + "the whole of the access decision");

        ReceiptReferenceGenerator.CredentialBits.Should().BeGreaterThanOrEqualTo(128);

        suffix.ToCharArray().Should().OnlyContain(
            character => Alphabet.Contains(character, StringComparison.Ordinal),
            "the same alphabet, because the only difference between the two forms is length");
    }

    /// <summary>
    /// <b>What 30 bits actually bought, stated once in a test rather than in a report.</b> The
    /// difference between the two forms is 100 bits, which is the difference between an afternoon
    /// and never.
    /// </summary>
    [Fact]
    public void The_two_forms_differ_by_a_hundred_bits()
    {
        var handle = _generator.Next("DON", Noon, ReceiptReferenceStrength.Handle);
        var credential = _generator.Next("DON", Noon, ReceiptReferenceStrength.Credential);

        var handleBits = handle.Split('-')[2].Length * ReceiptReferenceGenerator.BitsPerCharacter;
        var credentialBits =
            credential.Split('-')[2].Length * ReceiptReferenceGenerator.BitsPerCharacter;

        handleBits.Should().Be(30, "which is what BBL-004 found, and is fine once ownership decides access");
        credentialBits.Should().Be(130);
    }

    /// <summary>
    /// <b>The property the bit count is a proxy for.</b> A generator that returned a constant, or
    /// drew from four of its thirty-two symbols, would satisfy every length assertion above. Over
    /// 2,000 draws every symbol must appear and every reference must be distinct; neither holds for
    /// a degenerate source, and both hold overwhelmingly for a uniform one.
    /// </summary>
    [Fact]
    public void Every_symbol_is_reachable_and_no_two_references_repeat()
    {
        var references = Enumerable
            .Range(0, 2_000)
            .Select(_ => _generator.Next("DON", Noon, ReceiptReferenceStrength.Credential))
            .ToList();

        references.Distinct(StringComparer.Ordinal).Should().HaveCount(
            references.Count, "130 bits does not collide in two thousand draws");

        var drawn = references
            .SelectMany(reference => reference.Split('-')[2])
            .ToHashSet();

        drawn.Should().HaveCount(
            Alphabet.Length,
            "a source that reached only part of its alphabet would carry fewer bits than its "
            + "length claims, which is the failure a length assertion cannot see");
    }

    /// <summary>
    /// The short form is random too, which is easy to lose sight of: it was carved out of a
    /// sequential GUID and the class was called <c>SequentialReceiptReferenceGenerator</c>. Nothing
    /// orders receipts by their suffix — the date does that — so a predictable one would have bought
    /// nothing and cost the collision margin the unique index relies on.
    /// </summary>
    [Fact]
    public void Two_handles_minted_in_the_same_moment_differ()
    {
        var references = Enumerable
            .Range(0, 100)
            .Select(_ => _generator.Next("DON", Noon, ReceiptReferenceStrength.Handle))
            .ToHashSet(StringComparer.Ordinal);

        references.Should().HaveCountGreaterThan(
            90, "30 bits collides about once in 34 million pairs, not once in a hundred");
    }

    [Fact]
    public void A_missing_prefix_becomes_REF_rather_than_an_empty_segment()
    {
        _generator.Next("   ", Noon, ReceiptReferenceStrength.Handle)
            .Should().StartWith("REF-20261117-");

        _generator.Next("con", Noon, ReceiptReferenceStrength.Handle)
            .Should().StartWith("CON-", "a prefix is upper-cased so one cause cannot have two forms");
    }

    /// <summary>
    /// The date is the caller's, in UTC. A reference minted from local time would put two references
    /// for the same moment on different days, and the collision margin is per day.
    /// </summary>
    [Fact]
    public void The_date_is_the_UTC_date_of_the_moment_it_was_minted()
    {
        var lateInNewZealand = new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.FromHours(13));

        _generator.Next("DON", lateInNewZealand, ReceiptReferenceStrength.Handle)
            .Should().StartWith("DON-20261117-", "21:00 UTC on the 17th");
    }
}
