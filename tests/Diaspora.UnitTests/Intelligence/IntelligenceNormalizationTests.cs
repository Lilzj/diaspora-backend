using Diaspora.Modules.Guides.Domain.Intelligence;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Intelligence;

/// <summary>
/// The three kinds of text Country Intelligence normalizes that no earlier phase needed.
/// <para>
/// Keys, country codes and locales delegate to the Knowledge Directory's normalizer, and the last
/// test here is what stops that becoming two implementations: a key normalized differently on the two
/// sides of a join produces a category the database says does not exist, with both spellings
/// plausible and nothing in a stack trace.
/// </para>
/// </summary>
public class IntelligenceNormalizationTests
{
    // ── URLs ────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://pio.govmu.org/circular")]
    [InlineData("http://example.gov.ng/notice?id=4")]
    public void An_absolute_http_or_https_url_is_accepted(string url)
    {
        IntelligenceNormalization.Url(url).Should().NotBeNull();
    }

    /// <summary>
    /// Two schemes and no others. <c>javascript:</c> and <c>data:</c> are what turn an editorial
    /// field into an injection surface, and refusing everything except the two a government portal
    /// actually uses is a shorter list to get right than the list of things to block.
    /// </summary>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("ftp://files.gov/notice.pdf")]
    [InlineData("file:///etc/passwd")]
    [InlineData("/relative/path")]
    [InlineData("notice.pdf")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_else_is_refused(string? url)
    {
        IntelligenceNormalization.Url(url).Should().BeNull();
    }

    /// <summary>
    /// The host lower-cases and the rest does not. Query strings on government portals are frequently
    /// case-sensitive, and helpfully normalizing one is how a link that worked in the editor 404s in
    /// production.
    /// </summary>
    [Fact]
    public void The_host_lower_cases_and_the_query_string_does_not()
    {
        var normalized = IntelligenceNormalization.Url("HTTPS://PIO.GOVMU.ORG/Forms?Ref=AbC");

        normalized.Should().Be("https://pio.govmu.org/Forms?Ref=AbC");
    }

    [Fact]
    public void The_host_is_extracted_for_a_source_policy_to_inspect()
    {
        IntelligenceNormalization.Host("HTTPS://PIO.GOVMU.ORG:443/x")
            .Should().Be("pio.govmu.org");

        IntelligenceNormalization.Host("not a url").Should().BeNull();
    }

    // ── Telephone numbers ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Deliberately not E.164.</b> An emergency number is frequently not a dialable international
    /// number at all, and a normalizer that insisted on a country code would either mangle them or
    /// refuse them.
    /// </summary>
    [Theory]
    [InlineData("112", "112")]
    [InlineData("999", "999")]
    [InlineData("  +230  210 9312  ", "+230 210 9312")]
    [InlineData("(+234) 1-234-5678", "(+234) 1-234-5678")]
    public void A_number_keeps_its_shape(string input, string expected)
    {
        IntelligenceNormalization.Phone(input).Should().Be(expected);
    }

    /// <summary>
    /// <b>Letters are stripped, which mangles a written-out extension — and the field is not where
    /// one belongs.</b> "0800 123 456 ext. 4" becomes "0800 123 456 . 4": the digits a dialer needs
    /// survive and the prose does not. An extension goes in the contact's note, where it is rendered
    /// as written; keeping letters here would mean the field could hold "call the switchboard", which
    /// a client would try to dial.
    /// </summary>
    [Fact]
    public void Letters_are_stripped_so_the_field_can_only_hold_something_dialable()
    {
        IntelligenceNormalization.Phone("0800 123 456 ext. 4")
            .Should().Be("0800 123 456 . 4");

        IntelligenceNormalization.Phone("call the switchboard").Should().BeNull();
    }

    [Theory]
    [InlineData("no digits")]
    [InlineData("   ")]
    [InlineData("")]
    [InlineData(null)]
    public void Something_with_no_digit_in_it_is_not_a_number(string? input)
    {
        IntelligenceNormalization.Phone(input).Should().BeNull();
    }

    // ── Email ───────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("  INFO@Pio.GovMu.Org ", "info@pio.govmu.org")]
    [InlineData("consular@example.gov.ng", "consular@example.gov.ng")]
    public void An_address_lower_cases(string input, string expected)
    {
        IntelligenceNormalization.Email(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("not an address")]
    [InlineData("two@at@signs.com")]
    [InlineData("@nolocal.com")]
    [InlineData("nodomain@")]
    [InlineData("no.at.sign.com")]
    [InlineData("trailing@dot.")]
    public void An_unusable_address_is_refused(string input)
    {
        IntelligenceNormalization.Email(input).Should().BeNull();
    }

    // ── Reference numbers ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Case is preserved. Reference numbers are quoted back to the issuing office by members who
    /// need to be told what the office recognises, and <c>MHA/IMM/2026/014</c> lower-cased is a
    /// string a clerk will not find.
    /// </summary>
    [Fact]
    public void A_reference_number_keeps_its_case()
    {
        IntelligenceNormalization.Reference("  MHA/IMM/2026/014  ")
            .Should().Be("MHA/IMM/2026/014");
    }

    // ── Delegation ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>One key grammar across the module, and this is what holds it.</b> Notice categories, guide
    /// categories and navigation rows all share it; a key normalized in two of them and left alone in
    /// the third produces a row the database says does not exist, with both spellings plausible.
    /// </summary>
    [Theory]
    [InlineData("  Visa-Advisories  ", "visa-advisories")]
    [InlineData("EMERGENCY-ALERTS", "emergency-alerts")]
    public void Keys_country_codes_and_locales_match_the_knowledge_directory_exactly(
        string input, string expected)
    {
        IntelligenceNormalization.Key(input)
            .Should().Be(Modules.Guides.Domain.Knowledge.KnowledgeNormalization.Key(input));

        IntelligenceNormalization.Key(input).Should().Be(expected);

        IntelligenceNormalization.Country("  mu ")
            .Should().Be(Modules.Guides.Domain.Knowledge.KnowledgeNormalization.Country("  mu "));

        IntelligenceNormalization.Locale("FR-mu")
            .Should().Be(Modules.Guides.Domain.Knowledge.KnowledgeNormalization.Locale("FR-mu"));
    }
}
