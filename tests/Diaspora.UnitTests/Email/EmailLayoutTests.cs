using Diaspora.Email;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Email;

/// <summary>
/// The house layout, and the escaping that lets it exist at all.
/// </summary>
/// <remarks>
/// <para>
/// The reason <c>EmailNotificationChannel</c> refuses to produce HTML is that template bodies are
/// <b>editable reference data</b> — an operator changes one through the admin API, with no deploy
/// and no review. Rendering that into an email as markup would be stored cross-site scripting with
/// a mailing list attached.
/// </para>
/// <para>
/// So the trade this layout makes is: all the markup is here, in code, and everything that comes
/// from a template is escaped on the way in. These tests are what keeps that true.
/// </para>
/// </remarks>
public sealed class EmailLayoutTests
{
    // ── The escaping ────────────────────────────────────────────────────────

    [Fact]
    public void Markup_in_a_template_body_is_escaped_rather_than_rendered()
    {
        var html = EmailLayout.Html("Hello", "<script>alert('x')</script>");

        html.Should().NotContain("<script>");
        html.Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public void Markup_in_a_title_is_escaped_too()
    {
        // The title reaches two places — the <title> element and the heading — and an escape that
        // covered only one of them would be worse than none, because it would look handled.
        var html = EmailLayout.Html("<img src=x onerror=alert(1)>", "A body");

        html.Should().NotContain("<img");
        html.Should().Contain("&lt;img");
    }

    [Fact]
    public void An_action_label_is_escaped()
    {
        var html = EmailLayout.Html(
            "Title", "Body", actionLabel: "<b>Go</b>", actionUrl: "https://diaspora.test/x");

        html.Should().NotContain("<b>Go</b>");
        html.Should().Contain("&lt;b&gt;Go&lt;/b&gt;");
    }

    // ── The link, which is the other way markup gets in ─────────────────────

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("/housing/123")]
    [InlineData("")]
    [InlineData(null)]
    public void A_url_that_is_not_absolute_http_is_refused(string? url)
    {
        // Routes come from reference data as well. A relative one cannot resolve in a mail client,
        // and the scheme check is what stops `javascript:` reaching an href.
        var html = EmailLayout.Html("Title", "Body", actionLabel: "Open", actionUrl: url);

        html.Should().NotContain("<a href");
    }

    [Fact]
    public void An_absolute_http_url_becomes_a_button()
    {
        var html = EmailLayout.Html(
            "Title", "Body", actionLabel: "Open", actionUrl: "https://diaspora.test/housing/1");

        html.Should().Contain("https://diaspora.test/housing/1");
        html.Should().Contain("Open");
    }

    // ── Both parts carry the same facts ─────────────────────────────────────

    [Fact]
    public void The_text_part_carries_the_action_as_a_labelled_url()
    {
        // Not a stripped copy of the markup. A bare href with no label reads as noise in a text
        // client; the label is what tells somebody what they are about to open.
        var text = EmailLayout.Text(
            "Title", "Body", actionLabel: "Open in Diaspora", actionUrl: "https://diaspora.test/x");

        text.Should().Contain("Open in Diaspora:");
        text.Should().Contain("https://diaspora.test/x");
    }

    [Fact]
    public void The_text_part_is_not_html()
    {
        var text = EmailLayout.Text("Title", "A body", actionLabel: "Open", actionUrl: "https://a.test/x");

        text.Should().NotContain("<");
        text.Should().Contain("Title");
        text.Should().Contain("A body");
    }

    [Fact]
    public void Both_parts_carry_the_unsubscribe_link_when_there_is_one()
    {
        const string Url = "https://diaspora.test/unsubscribe/tok";

        EmailLayout.Html("T", "B", unsubscribeUrl: Url).Should().Contain(Url);
        EmailLayout.Text("T", "B", unsubscribeUrl: Url).Should().Contain(Url);
    }

    [Fact]
    public void A_transactional_message_carries_no_unsubscribe_link()
    {
        // A recovery code is not a message a preference governs. Offering to unsubscribe from it
        // would be offering to opt out of signing in.
        EmailLayout.Html("T", "B").Should().NotContain("Unsubscribe");
        EmailLayout.Text("T", "B").Should().NotContain("Unsubscribe");
    }

    // ── Readability ─────────────────────────────────────────────────────────

    [Fact]
    public void Blank_lines_become_paragraphs_and_single_breaks_survive()
    {
        // Composed copy relies on its line breaks. Collapsed into one block, a three-sentence
        // message becomes a wall, which is what a plain body rendered as HTML looks like.
        var html = EmailLayout.Html("T", "One.\n\nTwo.\nStill two.");

        html.Should().Contain("<p style=\"margin:0 0 12px 0;\">One.</p>");
        html.Should().Contain("Two.<br>Still two.");
    }

    [Fact]
    public void The_document_is_a_complete_html_message()
    {
        // Mail clients are not browsers: a fragment without a doctype and a charset renders
        // inconsistently, and non-ASCII in a subject or body is what exposes it.
        var html = EmailLayout.Html("Bienvenue à Diaspora", "Voilà.");

        html.Should().StartWith("<!doctype html>");
        html.Should().Contain("charset=\"utf-8\"");
        html.Should().Contain("Bienvenue à Diaspora");
    }
}
