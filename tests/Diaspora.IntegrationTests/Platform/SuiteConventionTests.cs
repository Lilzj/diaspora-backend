using System.Text.RegularExpressions;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// BARCH-009. One convention of this suite that was held by habit until it was broken.
/// <para>
/// <b>Do not derive a host from a fixture you share.</b> <c>WithWebHostBuilder</c> builds a second
/// host over the parent factory's <em>same</em> connection field, and disposing the derived factory
/// closes that connection. For the in-memory SQLite database this suite runs on, the first sentence
/// of <see cref="CustomWebApplicationFactory"/> is then decisive: the database is destroyed the
/// moment its last connection closes. In a class fixture that is harmless, because the host is
/// ending anyway. <b>In a collection fixture it destroys the database for every class scheduled
/// after it.</b>
/// </para>
/// <para>
/// Measured, on 17 November 2026, by writing exactly that: one test in the Platform collection
/// calling <c>_factory.WithWebHostBuilder(…)</c> turned 20 tests across four other classes red, in
/// milliseconds, against a database that no longer existed. Every other caller in the suite is in a
/// class with no <c>[Collection]</c> — correct, and nowhere stated.
/// </para>
/// <para>
/// No host and no fixture: this reads the source tree, the way
/// <c>RouteAuthorizationCoverageTests</c> reads its baseline.
/// </para>
/// </summary>
public sealed class SuiteConventionTests
{
    /// <summary>
    /// The three ways the suite spells "the fixture I was handed": a backing field, the
    /// <c>Factory</c> property on <c>IdentityTestCore</c>, and an explicit <c>this</c>. A local
    /// variable from <c>new CustomWebApplicationFactory()</c> is deliberately not matched — deriving
    /// from a factory the test owns and nobody shares is the fix, not the defect.
    /// </summary>
    private static readonly Regex DerivesFromAShared = new(
        @"(?:this\.\w+|_\w+|\bFactory)\s*\.\s*WithWebHostBuilder",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// Comments removed before matching, because the first version of this rule failed on its own
    /// documentation: the doc comment above quotes the offending call in order to explain it, and a
    /// rule that reads raw text cannot tell an explanation from an instance.
    /// <para>
    /// A rule you have to avoid describing in prose will fire on whoever describes it next, so the
    /// fix belongs here rather than in the wording.
    /// </para>
    /// </summary>
    private static string WithoutComments(string text)
    {
        var withoutBlocks = Regex.Replace(
            text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline, TimeSpan.FromSeconds(5));

        // "://" is spared so a URL in a string literal does not swallow the rest of its line.
        return Regex.Replace(
            withoutBlocks,
            @"(?<!:)//.*$",
            string.Empty,
            RegexOptions.Multiline,
            TimeSpan.FromSeconds(5));
    }

    private static IReadOnlyList<(string Name, string Text)> SuiteFiles()
    {
        var root = Path.Combine(
            RepositoryRoot.Current.FullName, "tests", "Diaspora.IntegrationTests");

        return
        [
            .. Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Select(path => (
                    Name: Path.GetFileName(path),
                    Text: WithoutComments(File.ReadAllText(path))))
        ];
    }

    /// <summary>
    /// The sanity check the rule below rests on. A scan whose directory walk has broken reads no
    /// files and forbids nothing, and the only symptom is a green test — the failure this
    /// programme has now hit twice.
    /// </summary>
    [Fact]
    public void The_scan_reads_this_suite()
    {
        var files = SuiteFiles();

        files.Should().HaveCountGreaterThan(200, "the suite is over two hundred files");

        files.Select(file => file.Name).Should().Contain(
            "CustomWebApplicationFactory.cs", "or the scan is not looking at this suite");

        files.Where(file => file.Text.Contains("WithWebHostBuilder", StringComparison.Ordinal))
            .Should().HaveCountGreaterThan(
                2,
                "the rule below exists to constrain these callers, so finding none would mean it "
                + "was passing over an empty set");
    }

    [Fact]
    public void No_class_in_a_collection_derives_a_host_from_the_fixture_it_shares()
    {
        var offenders = SuiteFiles()
            .Where(file => file.Text.Contains("[Collection(", StringComparison.Ordinal))
            .Where(file => DerivesFromAShared.IsMatch(file.Text))
            .Select(file => file.Name)
            .ToList();

        offenders.Should().BeEmpty(
            "WithWebHostBuilder over a shared fixture's connection closes the collection's "
            + "database when the derived factory is disposed, and every class scheduled after it "
            + "fails in milliseconds. Either give the class its own host by dropping "
            + "[Collection], or derive from a factory the test constructs and owns. Offending "
            + "files: {0}",
            string.Join(", ", offenders));
    }
}
