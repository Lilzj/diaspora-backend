using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The platform guides, checked against the platform.
/// <para>
/// <b>B1.4's F4 was a documentation defect that survived three verticals.</b>
/// <c>PermissionDefinition</c>'s comment said keys took the form <c>module:resource:action</c>;
/// eighty-eight shipped keys had two segments, every one of them correct, and the comment was wrong
/// for as long as nothing compared it to anything. Correcting it in B1.4.1 fixes the instance. This
/// suite is the part that stops the next one: a guide that quotes a permission key, a policy name or
/// a route prefix is quoting something the platform either has or does not.
/// </para>
/// <para>
/// <b>Deliberately narrow.</b> These rules check identifiers that the catalogues can adjudicate —
/// keys, policy names, prefixes — and say nothing about prose, which is where most documentation rot
/// lives and where a test would produce noise instead of signal. A rule that tried to check whether
/// a paragraph was still true would be wrong more often than the paragraph.
/// </para>
/// </summary>
public sealed class DocumentationSyncArchitectureTests
{
    /// <summary>
    /// Templates the guides use to describe a shape rather than to name a real key. Excluded by the
    /// literal string, not by a pattern, so a genuine key that happened to start with the word
    /// "module" would still be checked.
    /// </summary>
    private static readonly string[] Templates =
    [
        "module:action",
        "module:resource:action",
        "module:resource:action:scope"
    ];

    /// <summary>
    /// A permission key in a guide is a key the platform declares.
    /// <para>
    /// The failure this catches is the one F4 was: an example that was true when it was written and
    /// is now describing a key nobody ships, read by somebody copying it into a new module.
    /// </para>
    /// </summary>
    [DocumentationTheory]
    [InlineData("docs/platform/permission-guide.md")]
    [InlineData("docs/platform/authorization-guide.md")]
    public void Every_permission_key_quoted_in_a_guide_is_one_the_platform_declares(string document)
    {
        var declared = AuthorizationModel.Permissions
            .Select(permission => permission.Key)
            .ToHashSet(StringComparer.Ordinal);

        var quoted = InlineCode(document)
            .Where(token => token.Contains(':', StringComparison.Ordinal))
            .Where(token => Regex.IsMatch(
                token,
                "^[a-z][a-z0-9.-]*(:[a-z0-9][a-z0-9-]*)+$",
                RegexOptions.None,
                TimeSpan.FromSeconds(2)))
            .Where(token => Array.IndexOf(Templates, token) < 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        quoted.Should().NotBeEmpty("{0} documents permission keys and should show real ones", document);

        quoted.Where(token => !declared.Contains(token))
            .Should().BeEmpty(
                "{0} quotes a permission key no module declares — an example somebody will copy",
                document);
    }

    /// <summary>
    /// A policy name in a guide is a policy the platform declares, or a legacy name it still
    /// answers to. The second half matters: the migration table in the Authorization Guide names six
    /// policies that no longer exist as policies, and a rule that did not know about aliases would
    /// report the very documentation that explains them.
    /// </summary>
    [DocumentationFact]
    public void Every_policy_name_quoted_in_the_authorization_guide_resolves()
    {
        var quoted = InlineCode("docs/platform/authorization-guide.md")
            .Where(token => Regex.IsMatch(
                token,
                "^[A-Za-z][A-Za-z0-9-]*(\\.[A-Za-z0-9][A-Za-z0-9-]*)+$",
                RegexOptions.None,
                TimeSpan.FromSeconds(2)))
            // Filenames and type members share the shape. A policy name is the thing the catalogue
            // knows about, so anything it does not recognise is only a failure if it looks like a
            // policy the platform was supposed to have — which the migration table is the test of.
            .Where(token => token.StartsWith("notifications.", StringComparison.OrdinalIgnoreCase) ||
                            token.StartsWith("recommendations.", StringComparison.OrdinalIgnoreCase) ||
                            token.StartsWith("identity.state.", StringComparison.OrdinalIgnoreCase) ||
                            token.StartsWith("housing.", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        quoted.Should().NotBeEmpty("the guide documents the B1.4.1 rename by name");

        quoted.Where(token => AuthorizationModel.FindPolicy(token) is null)
            .Should().BeEmpty(
                "the guide names a policy that neither exists nor is a legacy name still answered");
    }

    /// <summary>
    /// The route prefixes the API Style Guide holds up as examples are the ones being served. The
    /// guide is where somebody looks before writing their first endpoint contributor, so a prefix
    /// that has drifted is copied into the next module.
    /// </summary>
    [DocumentationFact]
    public void Every_route_prefix_quoted_in_the_api_style_guide_is_declared()
    {
        var declared = DeclaredPrefixes();

        var quoted = InlineCode("docs/platform/api-style-guide.md")
            .Where(token => token.StartsWith("/api/v", StringComparison.Ordinal))
            .Where(token => !token.Contains("v2", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        quoted.Should().NotBeEmpty("the guide shows prefixes by name");

        quoted.Where(token => !declared.Contains(token))
            .Should().BeEmpty("the guide quotes a route prefix no module declares");
    }

    /// <summary>
    /// The corrected <c>PermissionDefinition</c> comment describes all three shapes the catalogue
    /// uses. This is F4's fix, asserted where it was made rather than only where it was explained —
    /// the guide can be updated without the comment, and the comment is what somebody reads at the
    /// declaration site.
    /// </summary>
    [Fact]
    public void The_permission_definition_comment_documents_every_shape_in_use()
    {
        var source = File.ReadAllText(Path.Combine(
            SourceTree.Root.FullName,
            "src/Diaspora.Contracts/Authorization/PermissionDefinition.cs"));

        source.Should().Contain(
            "module:action",
            "eighty-eight shipped keys have two segments and the comment used to deny it");
        source.Should().Contain("module:resource:action");
        source.Should().Contain(
            "module:resource:action:scope",
            "twenty-nine keys separate authority over your own rows from authority over anybody's");
    }

    private static IReadOnlyList<string> InlineCode(string relativePath)
    {
        var path = Path.Combine(SourceTree.Root.FullName, relativePath);

        File.Exists(path).Should().BeTrue("{0} is a guide this suite keeps synchronised", relativePath);

        return [.. Regex
            .Matches(
                File.ReadAllText(path),
                "`([^`\n]+)`",
                RegexOptions.None,
                TimeSpan.FromSeconds(5))
            .Select(match => match.Groups[1].Value.Trim())];
    }

    private static HashSet<string> DeclaredPrefixes()
    {
        var prefixes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in SourceTree.ProductionFiles
            .Where(file => file.Path.EndsWith("ModuleMetadata.cs", StringComparison.Ordinal)))
        {
            foreach (Match match in Regex.Matches(
                file.Root.ToString(),
                "RoutePrefix\\s*=\\s*\"([^\"]+)\"",
                RegexOptions.None,
                TimeSpan.FromSeconds(5)))
            {
                prefixes.Add(match.Groups[1].Value);
            }
        }

        return prefixes;
    }
}
