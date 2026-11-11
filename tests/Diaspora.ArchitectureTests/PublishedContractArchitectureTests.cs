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
    /// <b>The contract declares no security schemes, and that is BSEC-011 and BBL-003 rather than
    /// a surprise.</b>
    /// <para>
    /// Asserted as it stands, deliberately. The document cannot say which of its 1,092 operations
    /// need a bearer token, so a client generated from it gets no authorization at all — and this
    /// test is what makes closing those two findings turn a green rule red, with a message saying
    /// which way to update it. A rule that silently started passing for the right reason would
    /// record nothing.
    /// </para>
    /// </summary>
    [Fact]
    public void The_contract_declares_no_security_schemes_which_is_a_tracked_finding()
    {
        var schemes = Contract.Value.RootElement.TryGetProperty("components", out var components)
                      && components.TryGetProperty("securitySchemes", out var declared)
            ? declared.EnumerateObject().Select(scheme => scheme.Name).ToList()
            : [];

        schemes.Should().BeEmpty(
            "BSEC-011 and BBL-003 are open against exactly this. When a bearer scheme is added, "
            + "this test is the one to delete — and its deletion is the record that the findings "
            + "were closed deliberately rather than by a document that quietly changed shape");
    }

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
