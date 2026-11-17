using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The committed OpenAPI contract, which a second repository reads (BBL-006).
/// <para>
/// <b>Why the backend needs rules about a file its own build writes.</b> The eleven
/// contract-drift suites that compare client assumptions against this document live in the
/// <c>Diaspora</c> repository and <c>describe.skip</c> themselves when the backend is not checked
/// out beside them — which it never is in the client's <c>static-and-unit</c> CI job. So the
/// platform's only automated client/server contract check runs on a developer's machine and
/// nowhere else, and the skip is silent: Jest reports "1 skipped" among 392 passing suites.
/// </para>
/// <para>
/// Two of that finding's three remediation steps are in the client's repository and are not in this
/// change. The third is here: the backend had no test asserting its own committed contract was
/// current either, so a stale <c>contracts/Diaspora.Api.json</c> could be merged from this side
/// just as silently. The <b>CI step</b> beside the Release build is the direct guarantee — the
/// build regenerates the document, so a dirty <c>contracts/</c> afterwards is a contract that was
/// committed stale. These tests cover what a diff cannot say.
/// </para>
/// <para>
/// <b>They also move the audit's own investigation into code.</b> BBL-006 opens by retracting an
/// earlier finding: a 3-operation gap between this document and the client's generated
/// <c>served-routes.ts</c> looked like drift and was not, because the client's generator excludes
/// every path outside <c>/api/v1/</c> and exactly three operations qualify. Establishing that took
/// reading a JavaScript file in another repository. <see cref="The_only_operations_outside_the_versioned_namespace_are_the_hosts_own_html_surfaces"/>
/// names those three, so the next person does not have to — and a fourth becomes a failing test
/// rather than a puzzle.
/// </para>
/// <para>
/// <b>Measured 11 November 2026: the drift BBL-006 describes is live.</b> This document carries
/// 1,092 operations, so the client's snapshot should hold 1,089 after its three exclusions. It
/// holds <b>1,087</b>, and the two missing entries are <c>POST /auth/step-up</c> and
/// <c>POST /auth/step-up/verify</c> — added by BSEC-007 and confirmed absent by name. The suite
/// written to catch exactly that skipped itself in the client's CI when BSEC-007 merged, and
/// nothing anywhere reported it. The consequence is the one <c>served-routes.ts</c> warns about in
/// its own header: a route absent from the list is not passed through to the network, so both
/// step-up endpoints are answerable from a fixture in a preview build today.
/// </para>
/// <para>
/// <b>17 November 2026 — the document now describes its own authorization (BSEC-011, BBL-003).</b>
/// It did not before: 1,092 operations, no security schemes, no document default and not one
/// operation declaring anything, so every client generated from it sent no <c>Authorization</c>
/// header anywhere. The rules below hold both of that finding's verification criteria — every
/// operation carries an explicit answer, and the operations needing no token are named rather than
/// counted. The test that used to assert the absence is gone; see
/// <see cref="The_contract_declares_one_bearer_scheme"/> for why its deletion is the record.
/// </para>
/// </summary>
public class PublishedContractArchitectureTests
{
    private const string ContractPath = "contracts/Diaspora.Api.json";

    private static readonly Lazy<JsonDocument> Contract = new(Load);

    /// <summary>
    /// The sanity check the rest rest on. A document this test cannot read is a test that passes by
    /// finding nothing.
    /// </summary>
    [Fact]
    public void The_contract_is_committed_and_parses()
    {
        var root = Contract.Value.RootElement;

        root.TryGetProperty("openapi", out var version).Should().BeTrue();
        version.GetString().Should().StartWith("3.");

        Paths().Should().HaveCountGreaterThan(
            500, "the host composes around 1,100 endpoints across 21 modules");
    }

    /// <summary>
    /// <b>Exactly three operations sit outside <c>/api/v1/</c>, and they are the host's own HTML
    /// surfaces.</b>
    /// <para>
    /// This is the fact that made a count mismatch look like drift. The client's
    /// <c>generate-served-routes.js</c> skips any path not under <c>/api/v1/</c>, so its snapshot is
    /// three entries shorter than this document's operation count and always will be. Nothing on
    /// either side said so; it had to be read out of the generator.
    /// </para>
    /// <para>
    /// Named individually rather than counted, because the useful failure is not "there are four
    /// now" but "there is a fourth and it is this one". A new unversioned route is a decision about
    /// what the client's generator will silently omit, and it belongs in a diff.
    /// </para>
    /// </summary>
    [Fact]
    public void The_only_operations_outside_the_versioned_namespace_are_the_hosts_own_html_surfaces()
    {
        string[] expected =
        [
            "/",
            "/payments/checkout/cancelled",
            "/payments/checkout/complete"
        ];

        Paths()
            .Where(path => !path.StartsWith("/api/v1/", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Should().BeEquivalentTo(
                expected,
                "the client's served-routes generator excludes everything outside /api/v1/, so a "
                + "fourth unversioned path would be absent from its snapshot and mockable again in "
                + "a preview build — which is the failure served-routes-drift exists to catch, and "
                + "it does not run in the client's CI");
    }

    /// <summary>
    /// Every path is absolute and carries at least one operation.
    /// </summary>
    /// <remarks>
    /// A path item with no method is a route the generator saw and could not describe, and it
    /// would be counted by one side of a drift comparison and not the other.
    /// </remarks>
    [Fact]
    public void Every_path_is_absolute_and_describes_at_least_one_operation()
    {
        Paths().Should().OnlyContain(path => path.StartsWith('/'));

        var empty = Contract.Value.RootElement.GetProperty("paths")
            .EnumerateObject()
            .Where(path => !path.Value.EnumerateObject().Any(member => IsOperation(member.Name)))
            .Select(path => path.Name)
            .ToList();

        empty.Should().BeEmpty("a path item with no operation describes nothing");
    }

    /// <summary>
    /// <b>The contract declares exactly one security scheme, and it is HTTP bearer — BSEC-011 and
    /// BBL-003, closed 17 November 2026.</b>
    /// <para>
    /// This replaces a test that asserted the opposite. That one held the absence in place on
    /// purpose, so that closing the findings would turn a green rule red rather than let the
    /// document quietly change shape; deleting it is the record that the change was deliberate.
    /// </para>
    /// <para>
    /// The name is asserted as the literal <c>bearer</c> rather than read from
    /// <c>BearerSecuritySchemeTransformer.SchemeId</c>, because it is published surface. Every
    /// consumer generated from this document names it, so renaming the constant is a breaking
    /// change to them and should fail here rather than compile quietly.
    /// </para>
    /// </summary>
    [Fact]
    public void The_contract_declares_one_bearer_scheme()
    {
        Schemes().Should().ContainSingle().Which.Name.Should().Be("bearer");

        var scheme = Schemes().Single().Value;

        scheme.GetProperty("type").GetString().Should().Be("http");
        scheme.GetProperty("scheme").GetString().Should().Be("bearer");
        scheme.GetProperty("bearerFormat").GetString().Should().Be("JWT");
    }

    /// <summary>
    /// <b>The document requires that scheme by default</b>, so the fallback for anything that
    /// loses its own declaration is "a token is needed".
    /// </summary>
    /// <remarks>
    /// Every operation states its own answer, which makes this redundant for a consumer that reads
    /// them — and that is the point. A tool that honours only document-level security, or a reader
    /// skimming the head of the file, is told the API needs a token, which is true of 1,028 of its
    /// 1,092 operations. Both halves fail closed in the same direction.
    /// </remarks>
    [Fact]
    public void The_document_requires_that_scheme_by_default()
    {
        var root = Contract.Value.RootElement;

        root.TryGetProperty("security", out var security).Should().BeTrue(
            "a document with no default leaves an operation that lost its own declaration reading "
            + "as public");

        security.EnumerateArray()
            .SelectMany(requirement => requirement.EnumerateObject().Select(scheme => scheme.Name))
            .Should().BeEquivalentTo(["bearer"]);
    }

    /// <summary>
    /// <b>Every operation says whether it needs a token.</b> This is the finding's first
    /// verification criterion.
    /// </summary>
    /// <remarks>
    /// An absent <c>security</c> means "inherit the default", which is indistinguishable from
    /// nobody having decided — the same ambiguity <c>[AllowAnonymous]</c> was introduced to remove
    /// in the CQRS layer. A new endpoint cannot be added to this platform without an answer here,
    /// because the transformer gives every operation one and this counts them.
    /// </remarks>
    [Fact]
    public void Every_operation_states_whether_it_needs_a_token()
    {
        var operations = Operations();

        // Asserted before the filter below, which an empty document would satisfy just as happily
        // as a correct one.
        operations.Should().HaveCountGreaterThan(
            1_000, "the host composes around 1,100 endpoints across 21 modules");

        operations
            .Where(operation => !operation.Value.TryGetProperty("security", out _))
            .Select(operation => operation.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .Should().BeEmpty("an operation with no security declaration has not been decided");
    }

    /// <summary>
    /// <b>Every requirement names the scheme the document defines.</b>
    /// </summary>
    /// <remarks>
    /// A requirement naming an unregistered scheme is a dangling reference: valid-looking JSON that
    /// no generator can resolve. It is the specific failure mode of composing this across two
    /// transformers — the operation transformer writes the name before the document transformer
    /// registers the component, because document transformers run last.
    /// </remarks>
    [Fact]
    public void Every_requirement_names_a_declared_scheme()
    {
        var declared = Schemes().Select(scheme => scheme.Name).ToHashSet(StringComparer.Ordinal);

        Operations()
            .SelectMany(operation => operation.Value.GetProperty("security").EnumerateArray()
                .SelectMany(requirement => requirement.EnumerateObject())
                .Where(scheme => !declared.Contains(scheme.Name))
                .Select(scheme => operation.Key + " -> " + scheme.Name))
            .Distinct()
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .Should().BeEmpty("this names a security scheme the document does not define");
    }

    /// <summary>
    /// <b>These are the operations a caller reaches without a token, by name.</b> This is the
    /// finding's second verification criterion, and the reason it is a list rather than a count.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The useful failure is not "there are 65 now" but "there is a new one and it is this". A
    /// route becoming public is a decision, and it belongs in a diff where somebody has to agree
    /// to it. The set is also the platform's published public surface — the thing BSEC-003 found
    /// nobody could enumerate.
    /// </para>
    /// <para>
    /// Four groups, and each is deliberate: the ten credential-exchange routes under
    /// <c>/auth</c>, which cannot require the token they exist to issue; the three provider
    /// webhooks, which are authenticated by payload signature rather than by a bearer; published
    /// discovery — communities, events, housing, marketplace, media content and the reference
    /// catalogues — which is public because the platform's purpose is that someone can read the
    /// answer before being asked to sign up; and the three surfaces the host maps itself, which
    /// are the only unversioned paths in the document and are named again by
    /// <see cref="The_only_operations_outside_the_versioned_namespace_are_the_hosts_own_html_surfaces"/>.
    /// </para>
    /// <para>
    /// <b>The one that is not discovery is the export download</b>, and it is worth knowing why it
    /// belongs: the share link's <c>token</c> query parameter is the credential, so the route is
    /// anonymous at the gateway while the handler answers only to that token or to the owner's own
    /// session. <c>security: []</c> is accurate — no declared scheme is required — and the
    /// parameter is in the contract for a reader to find.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_operations_reachable_without_a_token_are_these()
    {
        string[] expected =
        [
            // The three the host maps itself, and the reason this list is 67 rather than 64. All
            // three reach no request type, so nothing behind the route would refuse them either;
            // they declared nothing until BSEC-011 asked what the contract should say about them.
            "GET /",
            "GET /payments/checkout/cancelled",
            "GET /payments/checkout/complete",

            "GET /api/v1/analytics/exports/{exportId}/download",
            "GET /api/v1/communities",
            "GET /api/v1/communities/compare",
            "GET /api/v1/communities/{idOrSlug}",
            "GET /api/v1/communities/{idOrSlug}/capabilities",
            "GET /api/v1/communities/{idOrSlug}/initiatives",
            "GET /api/v1/communities/{idOrSlug}/initiatives/{initiativeId}",
            "GET /api/v1/communities/{idOrSlug}/initiatives/{initiativeId}/participants",
            "GET /api/v1/communities/{idOrSlug}/initiatives/{initiativeId}/updates",
            "GET /api/v1/communities/{idOrSlug}/notices",
            "GET /api/v1/communities/{idOrSlug}/roles",
            "GET /api/v1/communities/{idOrSlug}/rules",
            "GET /api/v1/communities/{idOrSlug}/statistics",
            "GET /api/v1/community-life/area/activity",
            "GET /api/v1/community-life/calendar",
            "GET /api/v1/community-life/catalogue",
            "GET /api/v1/community-life/events",
            "GET /api/v1/community-life/events/occurrences/{occurrenceId}",
            "GET /api/v1/community-life/events/series",
            "GET /api/v1/community-life/events/series/{idOrSlug}",
            "GET /api/v1/community-life/events/series/{seriesId}/occurrences",
            "GET /api/v1/community-life/events/{idOrSlug}",
            "GET /api/v1/community-life/neighborhoods/{areaId}/activity",
            "GET /api/v1/community-life/neighborhoods/{areaId}/events",
            "GET /api/v1/community-life/organizations",
            "GET /api/v1/community-life/organizations/{idOrSlug}",
            "GET /api/v1/creators/content-health/rubric",
            "GET /api/v1/housing/accommodation-types",
            "GET /api/v1/housing/amenities",
            "GET /api/v1/housing/area/insights",
            "GET /api/v1/housing/landlords/{idOrSlug}",
            "GET /api/v1/housing/landlords/{landlordId}/reviews",
            "GET /api/v1/housing/listing-options",
            "GET /api/v1/housing/listings/{idOrSlug}",
            "GET /api/v1/housing/neighborhoods",
            "GET /api/v1/housing/neighborhoods/insights",
            "GET /api/v1/housing/neighborhoods/{idOrSlug}",
            "GET /api/v1/housing/search",
            "GET /api/v1/jobs/employers/{idOrSlug}",
            "GET /api/v1/marketplace",
            "GET /api/v1/marketplace/categories",
            "GET /api/v1/marketplace/categories/{key}/attributes",
            "GET /api/v1/marketplace/listings/by-slug/{slug}",
            "GET /api/v1/marketplace/listings/{listingId}",
            "GET /api/v1/marketplace/sellers/by-slug/{slug}",
            "GET /api/v1/marketplace/sellers/{sellerId}",
            "GET /api/v1/marketplace/sellers/{sellerId}/reviews",
            "GET /api/v1/media/{mediaId}/content",
            "GET /api/v1/media/{mediaId}/variants/{variant}",
            "POST /api/v1/auth/email/change/confirm",
            "POST /api/v1/auth/email/confirm",
            "POST /api/v1/auth/forgot-password",
            "POST /api/v1/auth/login",
            "POST /api/v1/auth/password-check",
            "POST /api/v1/auth/refresh",
            "POST /api/v1/auth/register",
            "POST /api/v1/auth/resend-otp",
            "POST /api/v1/auth/reset-password",
            "POST /api/v1/auth/verify-phone",
            "POST /api/v1/financial/webhooks/{provider}",
            "POST /api/v1/financial/webhooks/{provider}/payouts",
            "POST /api/v1/integrations/webhooks/{provider}",
            "POST /api/v1/marketplace/search",
            "POST /api/v1/notifications/unsubscribe"
        ];

        Operations()
            .Where(operation => operation.Value.GetProperty("security").GetArrayLength() == 0)
            .Select(operation => operation.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .Should().BeEquivalentTo(
                expected,
                "an operation joining this list is a route that became publicly reachable, and one "
                + "leaving it is a public route that now demands a token — both are decisions, and "
                + "neither should reach a consumer without somebody agreeing to it here");
    }

    private static List<JsonProperty> Schemes() =>
        Contract.Value.RootElement.TryGetProperty("components", out var components)
        && components.TryGetProperty("securitySchemes", out var declared)
            ? [.. declared.EnumerateObject()]
            : [];

    /// <summary>
    /// Every operation in the document, keyed <c>METHOD /path</c> the way a route table reads.
    /// </summary>
    private static List<KeyValuePair<string, JsonElement>> Operations() =>
        [.. Contract.Value.RootElement.GetProperty("paths")
            .EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject()
                .Where(member => IsOperation(member.Name))
                .Select(member => new KeyValuePair<string, JsonElement>(
                    member.Name.ToUpperInvariant() + " " + path.Name,
                    member.Value)))];

    private static bool IsOperation(string name) =>
        name is "get" or "put" or "post" or "delete" or "patch" or "head" or "options" or "trace";

    private static List<string> Paths() =>
        [.. Contract.Value.RootElement.GetProperty("paths")
            .EnumerateObject()
            .Select(path => path.Name)];

    private static JsonDocument Load()
    {
        var path = Path.Combine(SourceTree.Root.FullName, ContractPath);

        File.Exists(path).Should().BeTrue(
            "{0} is a committed artefact a second repository reads, and the build writes it on "
            + "every compile of Diaspora.Api", ContractPath);

        return JsonDocument.Parse(File.ReadAllText(path));
    }
}
