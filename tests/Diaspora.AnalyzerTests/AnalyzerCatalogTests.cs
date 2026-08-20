using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using Diaspora.Analyzers;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Diaspora.AnalyzerTests;

/// <summary>
/// The catalogue checks itself.
/// <para>
/// <b>A rule that ships without a catalogue entry is a rule nobody can look up when it fires.</b>
/// The compiler already enforces half of this — <c>RS2008</c> refuses a diagnostic id missing from
/// the release-tracking file — and these tests close the other half: that the release file, the
/// published documentation and the analyzers themselves all describe the same set of rules, and
/// that every one of them says where to read more.
/// </para>
/// </summary>
public sealed class AnalyzerCatalogTests
{
    private static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers =
    [
        new DomainEventSafetyAnalyzer(),
        new MessagingBoundaryAnalyzer(),
        new PersistenceConventionAnalyzer(),
        new CqrsGuardrailAnalyzer(),
        new ContributorConventionAnalyzer(),
        new AiIsolationAnalyzer()
    ];

    private static IReadOnlyList<DiagnosticDescriptor> Descriptors =>
        [.. Analyzers.SelectMany(analyzer => analyzer.SupportedDiagnostics)];

    /// <summary>
    /// The whole pack is discovered by reflection, so a new analyzer added to the assembly and left
    /// out of the list above would be silently untested — and, because the list above is also what
    /// the harness runs, silently unexercised by every other test in this project.
    /// </summary>
    [Fact]
    public void Every_analyzer_in_the_assembly_is_under_test()
    {
        var declared = typeof(DomainEventSafetyAnalyzer).Assembly
            .GetTypes()
            .Where(type => type.IsSubclassOf(typeof(DiagnosticAnalyzer)) && !type.IsAbstract)
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal);

        Analyzers.Select(analyzer => analyzer.GetType().Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Should().Equal(declared);
    }

    [Fact]
    public void Every_diagnostic_id_is_declared_once()
    {
        var ids = Descriptors.Select(descriptor => descriptor.Id).ToList();

        ids.Should().OnlyHaveUniqueItems(
            "an id reported by two rules makes a suppression silence something its author did not " +
            "choose to silence");

        ids.Should().AllSatisfy(id => id.Should().MatchRegex("^DSP[0-9]{4}$"));
    }

    /// <summary>
    /// A diagnostic with no description and no link is a four-character code and a sentence, and the
    /// person reading it at the bottom of a build log has nowhere to go.
    /// </summary>
    [Fact]
    public void Every_diagnostic_explains_itself_and_says_where_to_read_more()
    {
        foreach (var descriptor in Descriptors)
        {
            descriptor.Title.ToString(CultureInfo.InvariantCulture)
                .Should().NotBeNullOrWhiteSpace();

            descriptor.Description.ToString(CultureInfo.InvariantCulture)
                .Should().NotBeNullOrWhiteSpace(
                "{0} must say what failure it prevents", descriptor.Id);

            descriptor.HelpLinkUri.Should().Contain(
                "b1.4-analyzer-catalog", "{0} must link the catalogue", descriptor.Id);

            descriptor.IsEnabledByDefault.Should().BeTrue(
                "{0} would otherwise ship switched off", descriptor.Id);
        }
    }

    /// <summary>
    /// The release-tracking file is what <c>RS2008</c> reads and what the catalogue document is
    /// written from. This asserts the third leg: that it lists exactly the rules that exist, with
    /// the severity they actually ship at.
    /// </summary>
    [Fact]
    public void The_release_file_lists_every_rule_at_its_real_severity()
    {
        var recorded = ReleaseEntries();

        recorded.Keys.OrderBy(id => id, StringComparer.Ordinal)
            .Should().Equal(Descriptors.Select(d => d.Id).OrderBy(id => id, StringComparer.Ordinal));

        foreach (var descriptor in Descriptors)
        {
            recorded[descriptor.Id].Should().Be(
                descriptor.DefaultSeverity.ToString(),
                "{0}'s recorded severity must be the one it ships at", descriptor.Id);
        }
    }

    /// <summary>
    /// And the fourth: the published catalogue names every rule. Written as a substring search
    /// rather than a parse, because the document is prose with a table in it and a rule that
    /// demanded a format would be a rule about markdown.
    /// </summary>
    [Fact]
    public void The_published_catalogue_documents_every_rule()
    {
        var document = File.ReadAllText(
            Path.Combine(RepositoryRoot().FullName, "docs", "platform", "b1.4-analyzer-catalog.md"));

        foreach (var descriptor in Descriptors)
        {
            document.Should().Contain(
                descriptor.Id, "the catalogue is where a build log's four-character code is looked up");
        }
    }

    private static Dictionary<string, string> ReleaseEntries()
    {
        var path = Path.Combine(
            RepositoryRoot().FullName, "src", "Diaspora.Analyzers", "AnalyzerReleases.Unshipped.md");

        var entries = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in File.ReadAllLines(path))
        {
            if (!line.StartsWith("DSP", StringComparison.Ordinal))
            {
                continue;
            }

            var columns = line.Split('|').Select(column => column.Trim()).ToArray();

            entries[columns[0]] = columns[2];
        }

        return entries;
    }

    private static DirectoryInfo RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (directory.GetFiles("Diaspora.slnx").Length > 0)
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not find Diaspora.slnx above " + AppContext.BaseDirectory + ".");
    }

    /// <summary>Kept so the reflection above cannot silently start finding nothing.</summary>
    [Fact]
    public void The_pack_is_not_empty()
    {
        Descriptors.Should().HaveCountGreaterThanOrEqualTo(
            15, "the phase shipped a pack, not a rule");
    }
}
