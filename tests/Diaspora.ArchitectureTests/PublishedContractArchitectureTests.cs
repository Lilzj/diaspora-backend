using System.Reflection;
using System.Text.Json;
using Diaspora.Platform.Modules;
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
            // The three the host maps itself, and the reason this list was 67 rather than 64. All
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

            // BSEC-004. Seven engagement writes, joining because they were always anonymous and
            // only now say so. The command behind each one has carried [AllowAnonymous] with its
            // reason since BSEC-003; the route carried nothing, so the contract's fail-closed
            // default told every client they needed a token for a counter bump. Declaring the
            // anonymity moves them here, where the decision is reviewable — which is the whole
            // point of pinning this list rather than counting it.
            //
            // They are the only entries here that are neither credential exchange nor a
            // signature-verified callback, so they are also the only ones a stranger can call
            // repeatedly to change what the platform shows. RateLimitPolicyNames.Engagement is
            // what bounds them.
            "POST /api/v1/guides/{guideId}/share",
            "POST /api/v1/guides/{guideId}/view",
            "POST /api/v1/integrations/webhooks/{provider}",
            "POST /api/v1/intelligence/notices/{noticeId}/activity",
            "POST /api/v1/knowledge/countries/{countryCode}/activity/completed",
            "POST /api/v1/knowledge/countries/{countryCode}/activity/discovered",
            "POST /api/v1/knowledge/countries/{countryCode}/activity/path-completed",
            "POST /api/v1/knowledge/countries/{countryCode}/activity/path-started",
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

    /// <summary>
    /// <b>Every 2xx response names the shape of its body, except the seventeen routes that do not
    /// answer the platform envelope (RMD-003).</b>
    /// <para>
    /// Until 20 November 2026 this document described <b>82</b> of its 1,093 2xx responses. The
    /// reason on record — in this repository and in the client's own
    /// <c>backend-openapi-contract-drift.test.ts</c> — was that minimal-API endpoints return
    /// <c>IResult</c>, so the generator has no return type to read. That was a symptom. The cause
    /// was that <c>[ProducesResponseType]</c> had been written 200 times in <c>Diaspora.Api</c>,
    /// whose MVC controllers return <c>Task&lt;IActionResult&gt;</c> and are exactly as untyped,
    /// and zero times across the 29 module projects. The document described a body wherever
    /// somebody had typed the type by hand.
    /// </para>
    /// <para>
    /// <b>The worse half was not the silence.</b> A generator with nothing to read emits a default
    /// success response, and the default is <c>200</c> — so all 89 <c>Created</c> endpoints were
    /// documented as answering 200 when they answer <b>201</b>. The document was not merely quiet
    /// about them, it was wrong, and a route-and-verb comparison cannot see a status code. Fixing
    /// that moved the histogram from <c>200: 1080 / 201: 1</c> to <c>200: 991 / 201: 90</c>.
    /// </para>
    /// <para>
    /// Named individually rather than counted, for the reason the rest of this file gives: the
    /// useful failure is not "there are eighteen now" but "there is an eighteenth and it is this
    /// one". An entry joining this list is a route that stopped describing its body — almost always
    /// because a handler stopped going through <c>PlatformResults</c>. An entry leaving it is a
    /// route that started, which is usually good and still worth a diff.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_successful_response_names_its_body_except_the_routes_that_send_no_envelope()
    {
        string[] expected =
        [
            "DELETE /api/v1/platform/operations/configuration/{key} [204]",       // bodiless by design
            "DELETE /api/v1/platform/operations/jobs/{jobId} [204]",              // bodiless by design
            "GET /api/v1/analytics/exports/{exportId}/download [200]",            // a file
            "GET /api/v1/media/{mediaId}/content [200]",                          // bytes
            "GET /api/v1/media/{mediaId}/variants/{variant} [200]",               // bytes
            "GET /payments/checkout/cancelled [200]",                             // the host's own HTML
            "GET /payments/checkout/complete [200]",                              // the host's own HTML
            "POST /api/v1/financial/webhooks/{provider} [200]",                   // a provider's callback
            "POST /api/v1/financial/webhooks/{provider}/payouts [200]",           // a provider's callback
            "POST /api/v1/integrations/webhooks/{provider} [200]",                // a provider's callback
            "POST /api/v1/platform/operations/cache/invalidate [204]",            // bodiless by design
            "POST /api/v1/platform/operations/kill-switch [204]",                 // bodiless by design
            "POST /api/v1/platform/operations/resilience/{dependency}/isolate [204]",  // bodiless
            "POST /api/v1/platform/operations/resilience/{dependency}/reset [204]",    // bodiless
            "POST /api/v1/telemetry/events [200]",                                // an acknowledgement
            "PUT /api/v1/platform/operations/configuration/{key} [204]",          // bodiless by design
            "PUT /api/v1/platform/operations/feature-flags/{flag} [204]"          // bodiless by design
        ];

        Operations()
            .SelectMany(operation => operation.Value.GetProperty("responses")
                .EnumerateObject()
                .Where(response => response.Name.StartsWith('2'))
                .Where(response => !NamesABody(response.Value))
                .Select(response => $"{operation.Key} [{response.Name}]"))
            .OrderBy(key => key, StringComparer.Ordinal)
            .Should().BeEquivalentTo(
                expected,
                "a 2xx that names no schema is a response shape no consumer can check and no gate "
                + "can see rename a field — which is the whole of RMD-003, found twice by hand "
                + "before anything was written down about it");
    }

    /// <summary>
    /// A schema name in this document stands for exactly one type.
    /// <para>
    /// <b>A component name in OpenAPI is global and a C# type name is not</b>, and until 23
    /// November 2026 nothing reconciled the two. Seventeen component names each stood for two or
    /// three different contract types; the generator emitted whichever it reached first, left the
    /// others out of the document entirely, and documented every route of the other modules with
    /// that one's properties. Measured against the corrected document, <b>147 of the 1,092
    /// operations</b> had been described with a shape that belonged to another module.
    /// </para>
    /// <para>
    /// <b>What that looked like in the one place anybody checked.</b> The document said
    /// <c>POST /api/v1/trust/verifications</c> took <c>{ notes }</c>; the route binds nine
    /// properties, and the client declares all nine — so a comparison of the two would have
    /// reported the client as wrong. <c>POST /api/v1/community/bookmarks</c> was described with
    /// the other bookmark module's body, which the client had found by hand and recorded in a
    /// comment reading "the published contract is wrong about this route". That comment was the
    /// whole of the platform's knowledge of this.
    /// </para>
    /// <para>
    /// <b>And the winner was discovery order.</b> Nothing chose CommunityProgramme's
    /// <c>MoneyResponse</c> over Financial's and Creator's — the generator met it first, and 93
    /// operations were documented with its shape when they return one of the others.
    /// <c>SchemaReferenceIds</c> qualifies an ambiguous name with its module and refuses to give
    /// one id to two types; this is the rule that fails if the qualifying is ever switched off,
    /// which takes that refusal with it.
    /// </para>
    /// <para>
    /// Ambiguity is read from the assemblies rather than from source, which is the same answer the
    /// generator computes and for the same reason: <c>SourcedValueResponse</c> and
    /// <c>SourcedValueResponse&lt;T&gt;</c> are two names to the runtime and one to a regex, and a
    /// source scan reports a collision that cannot happen.
    /// </para>
    /// </summary>
    [Fact]
    public void No_schema_name_in_the_contract_stands_for_more_than_one_type()
    {
        var declarations = TypeNamesByAssembly();

        EveryModuleContributedTypes(declarations);

        var ambiguous = declarations
            .SelectMany(entry => entry.Value.Select(name => (Assembly: entry.Key, Name: name)))
            .GroupBy(declaration => declaration.Name, StringComparer.Ordinal)
            .Where(group => group.Select(declaration => declaration.Assembly)
                .Distinct(StringComparer.Ordinal).Count() > 1)
            .ToDictionary(
                group => group.Key,
                group => group.Select(declaration => declaration.Assembly)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal);

        // Every name the document could be built from: what the platform declares, spelled the
        // way an id spells it (no arity), plus the qualified forms the generator synthesises.
        // Longest match wins, so `ModerationCaseReportResponse` is read as itself rather than as
        // a `ReportResponse` with a prefix, and `Community.BookmarkResponse` as itself rather than
        // as an unqualified one.
        var candidates = declarations.Values
            .SelectMany(names => names)
            .Select(WithoutArity)
            .Concat(ambiguous.SelectMany(entry =>
                entry.Value.Select(assembly => Segment(assembly) + "." + entry.Key)))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(name => name.Length)
            .ToList();

        var bare = SchemaNames()
            .Where(id => Tokenise(id, candidates).Any(ambiguous.ContainsKey))
            .Select(id => id + ": names "
                + string.Join(" and ", Tokenise(id, candidates)
                    .Where(ambiguous.ContainsKey)
                    .Select(name => $"'{name}' (declared by "
                        + string.Join(" and ", ambiguous[name]) + ")")))
            .Order(StringComparer.Ordinal)
            .ToList();

        bare.Should().BeEmpty(
            "a component this document names once must be one shape. Each of these is two or "
            + "more, so every route described with it is described with whichever type the "
            + "generator reached first — and the routes of the other module are documented as "
            + "sending and returning something they do not. SchemaReferenceIds qualifies an "
            + "ambiguous name with the module that owns it:"
            + Environment.NewLine + string.Join(Environment.NewLine, bare));
    }

    /// <summary>
    /// Splits a composed reference id into the type names it was built from, longest first.
    /// <para>
    /// <b>Reading the id as a whole is not enough, and the gap is the dangerous half.</b> A leaf
    /// schema named <c>BookmarkResponse</c> is obvious. A wrapper named
    /// <c>ApiResponseOfBookmarkResponse</c> is the same collision one level up — two different
    /// wrapper types claiming one component — and the document cannot show it, because a JSON
    /// object holds one value per key and the loser is simply not there. So the ambiguity has to
    /// be found inside the name.
    /// </para>
    /// <para>
    /// This reads the emitted document rather than re-running the generator's own rewrite, which
    /// is what keeps it a check: the two share an idea, not an implementation, and the thing
    /// asserted is the artefact clients read.
    /// </para>
    /// </summary>
    private static List<string> Tokenise(string id, List<string> candidates)
    {
        var tokens = new List<string>();

        for (var index = 0; index < id.Length;)
        {
            var match = candidates.Find(name =>
                string.CompareOrdinal(id, index, name, 0, name.Length) == 0);

            if (match is null)
            {
                index++;
                continue;
            }

            tokens.Add(match);
            index += match.Length;
        }

        return tokens;
    }

    /// <summary>
    /// The last segment of an assembly name, which is how <c>SchemaReferenceIds</c> spells the
    /// module in a qualified component name — joined with a dot, which no C# identifier can
    /// contain, so a qualified name can never be mistaken for some other type's own name.
    /// </summary>
    private static string Segment(string assembly) =>
        assembly[(assembly.LastIndexOf('.') + 1)..];

    /// <summary>
    /// A CLR name as a component id spells it: <c>PagedResult`1</c> appears as
    /// <c>PagedResult</c>.
    /// </summary>
    private static string WithoutArity(string name)
    {
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        return tick < 0 ? name : name[..tick];
    }

    /// <summary>
    /// The scan that rule rests on can see the whole platform.
    /// <para>
    /// Derived, never a floor. An assembly list that quietly came back short would empty the set
    /// of ambiguous names and leave the rule above passing over a document full of them — and
    /// "at least N assemblies" cannot tell a short list from a complete one. Every module project
    /// on disk must appear, so a module that stops being scanned fails by name.
    /// </para>
    /// </summary>
    private static void EveryModuleContributedTypes(
        Dictionary<string, HashSet<string>> declarations)
    {
        var onDisk = new DirectoryInfo(Path.Combine(SourceTree.Root.FullName, "src"))
            .EnumerateDirectories("Diaspora.Modules.*")
            .Select(directory => directory.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        onDisk.Should().NotBeEmpty("the solution has module projects under src/");

        onDisk
            .Where(module => !declarations.TryGetValue(module, out var types) || types.Count == 0)
            .Should().BeEmpty(
                "a module whose types the scan cannot see is a module whose collisions it cannot "
                + "report, and the rule it feeds would pass without them");
    }

    /// <summary>
    /// Every public type each platform assembly declares, by simple name.
    /// <para>
    /// <c>Type.Name</c> rather than the full name on purpose: the collision this guards against is
    /// between two types that a consumer would call by the same word, which is exactly what the
    /// document's component names are.
    /// </para>
    /// </summary>
    private static Dictionary<string, HashSet<string>> TypeNamesByAssembly()
    {
        var declarations = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var assembly in ModuleAssemblyScanner.FindPlatformAssemblies())
        {
            var name = assembly.GetName().Name;

            if (name is null)
            {
                continue;
            }

            Type[] types;

            try
            {
                types = assembly.GetExportedTypes();
            }
            catch (Exception exception) when (
                exception is FileNotFoundException or FileLoadException or TypeLoadException
                    or ReflectionTypeLoadException)
            {
                continue;
            }

            declarations[name] = [.. types.Select(type => type.Name)];
        }

        return declarations;
    }

    private static List<string> SchemaNames() =>
        Contract.Value.RootElement.TryGetProperty("components", out var components)
        && components.TryGetProperty("schemas", out var schemas)
            ? [.. schemas.EnumerateObject().Select(schema => schema.Name)]
            : [];

    /// <summary>
    /// A body is a schema, not a <c>content</c> block.
    /// </summary>
    /// <remarks>
    /// Eight responses in this document carry a media type with nothing under it. Counting blocks
    /// rather than schemas reported 90 described responses where 82 bodies were actually named, and
    /// the difference between those two numbers is the difference between a measurement and a
    /// number that looks like one.
    /// </remarks>
    private static bool NamesABody(JsonElement response) =>
        response.TryGetProperty("content", out var content)
        && content.EnumerateObject().Any(media => media.Value.TryGetProperty("schema", out _));

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
