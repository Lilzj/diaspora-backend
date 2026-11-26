using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// The host's own pages, and the Content-Security-Policy each is sent with (BSEC-010).
/// <para>
/// Four surfaces serve HTML from this host: the open-route hand-off, the two Stripe Checkout return
/// pages and the email confirmation pages. They run script, they are reached from email, and none
/// had a policy. These facts read only what a browser reads, the header and the page, and hold each
/// page to three things. Every inline stylesheet and script on it is admitted by the hash a browser
/// would compute, so the page renders as written. Nothing else is admitted, so a block that escaped
/// encoding would not run. And the page may do nothing it does not need: load nothing, post only
/// where its own form posts, keep its base URL, sit in no frame.
/// </para>
/// </summary>
[Collection(PlatformCollection.Name)]
public sealed partial class HostPagePolicyTests : PooledIdentityTestBase
{
    public HostPagePolicyTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>Every page the host serves, once for each difference in what a page holds.</summary>
    public static TheoryData<string, string> Pages => new()
    {
        { "GET", "/open/profile/trust-center" },       // hands off to the app
        { "GET", "/open/evil.com" },                     // the 404, which hands off nowhere
        { "GET", "/payments/checkout/complete" },
        { "GET", "/payments/checkout/cancelled" },
        { "GET", "/email/confirm?token=not-a-token" },   // a form that posts back here
        { "GET", "/email/confirm" },                     // the incomplete link
        { "POST", "/email/confirm" },                    // the outcome, with a link into the app
        { "GET", "/email/change?token=not-a-token" },
        { "GET", "/email/change" },
        { "POST", "/email/change" },
    };

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Every_page_admits_by_hash_exactly_the_style_and_script_it_carries(string method, string path)
    {
        var page = await RequestAsync(method, path);
        var policy = Directives(page);

        page.MediaType.Should().Be("text/html");

        Admitted(StyleBlock(), page.Html).Should().BeEquivalentTo(
            policy.GetValueOrDefault("style-src", []),
            "a stylesheet the policy does not name is not applied, and a hash the page does not "
            + "carry admits something it was never meant to");

        Admitted(ScriptBlock(), page.Html).Should().BeEquivalentTo(
            policy.GetValueOrDefault("script-src", []),
            "a script the policy does not name does not run, and a hash the page does not carry "
            + "admits something it was never meant to");
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Every_page_is_refused_everything_else(string method, string path)
    {
        var page = await RequestAsync(method, path);
        var policy = Directives(page);
        string[] directives = ["default-src", "style-src", "script-src", "base-uri", "form-action", "frame-ancestors"];

        policy.Keys.Should().BeSubsetOf(directives);
        policy["default-src"].Should().Equal("'none'");
        policy["base-uri"].Should().Equal("'none'");
        policy["frame-ancestors"].Should().Equal("'none'");
        policy["form-action"].Should().Equal(
            page.Html.Contains("<form", StringComparison.Ordinal) ? "'self'" : "'none'");

        policy.Values.SelectMany(sources => sources).Should().OnlyContain(
            source => source == "'none'" || source == "'self'" || source.StartsWith("'sha256-", StringComparison.Ordinal),
            "'unsafe-inline', a scheme or a host would admit something the page does not carry");

        InlineAttribute().IsMatch(page.Html).Should().BeFalse(
            "a hash admits a block, never an attribute, so an inline handler or style attribute "
            + "would be refused in silence");
    }

    [Fact]
    public async Task The_hand_off_follows_the_pages_own_link_so_one_hash_serves_every_destination()
    {
        var trustCentre = await RequestAsync("GET", "/open/profile/trust-center");
        var listing = await RequestAsync("GET", "/open/housing/123?tab=photos");

        ButtonLink(trustCentre.Html).Should().Be("diaspora://profile/trust-center");
        ButtonLink(listing.Html).Should().Be("diaspora://housing/123?tab=photos");

        ScriptBlock().Match(trustCentre.Html).Groups["text"].Value.Should().Contain(
            "document.querySelector(\"a.button\")",
            "nothing here runs the script, so this is what holds it to the element the page's link is");

        listing.Policies.Should().Equal(
            trustCentre.Policies,
            "the script carries no destination, so nothing about it can vary with one");
    }

    [Fact]
    public async Task A_response_that_is_not_a_page_is_sent_the_policy_that_forbids_everything()
    {
        var health = await RequestAsync("GET", "/health/live");

        health.Policies.Should().Equal(
            "default-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'");
    }

    /// <summary>What these facts read of a response: what a browser reads of it.</summary>
    private sealed record Page(string? MediaType, IReadOnlyList<string> Policies, string Html);

    private async Task<Page> RequestAsync(string method, string path)
    {
        using var stranger = Factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));

        if (method == HttpMethod.Post.Method)
        {
            request.Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("token", "not-a-token")]);
        }

        using var response = await stranger.SendAsync(request);

        return new Page(
            response.Content.Headers.ContentType?.MediaType,
            response.Headers.TryGetValues("Content-Security-Policy", out var values) ? [.. values] : [],
            await response.Content.ReadAsStringAsync());
    }

    /// <summary>The page's one policy, as each directive's name and its source expressions.</summary>
    private static Dictionary<string, string[]> Directives(Page page)
    {
        page.Policies.Should().ContainSingle(
            "a browser enforces every policy a response carries, so a second would refuse what the "
            + "first admits");

        return page.Policies[0]
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directive => directive.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToDictionary(tokens => tokens[0], tokens => tokens[1..], StringComparer.Ordinal);
    }

    /// <summary>
    /// The source expression each inline block needs, hashed as a browser hashes it: the block's
    /// text after the parser has turned every CR LF, then every lone CR, into LF.
    /// </summary>
    private static string[] Admitted(Regex blocks, string html) =>
    [
        .. blocks.Matches(html)
            .Select(match =>
            {
                match.Groups["attributes"].Value.Should().BeEmpty(
                    "the pages carry inline blocks only, which load nothing");

                var text = match.Groups["text"].Value
                    .Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Replace('\r', '\n');

                return "'sha256-" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(text))) + "'";
            })
            .Distinct(StringComparer.Ordinal)
    ];

    /// <summary>Where the page's button goes, which is where the hand-off script sends the browser.</summary>
    private static string ButtonLink(string html)
    {
        var match = ButtonAnchor().Match(html);

        match.Success.Should().BeTrue("the hand-off follows the page's button, so the page must have one");

        return WebUtility.HtmlDecode(match.Groups["href"].Value);
    }

    [GeneratedRegex("<style(?<attributes>[^>]*)>(?<text>.*?)</style>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex StyleBlock();

    [GeneratedRegex("<script(?<attributes>[^>]*)>(?<text>.*?)</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptBlock();

    [GeneratedRegex(@"<[a-z][^>]*\s(?:on[a-z]+|style)\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex InlineAttribute();

    [GeneratedRegex("<a class=\"button\" href=\"(?<href>[^\"]*)\">")]
    private static partial Regex ButtonAnchor();
}
