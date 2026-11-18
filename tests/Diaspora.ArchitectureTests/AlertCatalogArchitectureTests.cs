using System.Reflection;
using System.Text.RegularExpressions;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Contributors;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// Holds <c>src/Diaspora.Observability/AlertCatalog.md</c> to the instruments it describes.
/// <para>
/// <b>A catalogue that can name a metric nobody emits is the failure it exists to prevent.</b>
/// BARCH-008 asked for the first five alerts to be defined before launch, and the cheap version of
/// that is a document — which is exactly the shape the pre-launch audit kept finding: a comment
/// asserting a capability, with nothing checking it. These rules make the document fail the build
/// instead.
/// </para>
/// <para>
/// Three directions, because each catches a different way for it to rot. Forward: the catalogue
/// cannot name an instrument that does not exist. Inverse: an instrument whose own documentation
/// discusses alerting cannot be left out. And upward: a meter no contributor subscribes produces
/// nothing at runtime, so naming one would be a catalogue entry for a metric that never leaves the
/// process.
/// </para>
/// <para>
/// A plain <c>[Fact]</c>, not a <c>[DocumentationFact]</c>: this catalogue lives under
/// <c>src/</c> and is committed, so these rules run on every clean checkout. The eight rules that
/// read the gitignored <c>docs/</c> tree skip in CI and have always been vacuous there.
/// </para>
/// </summary>
public class AlertCatalogArchitectureTests
{
    private const string CatalogPath = "src/Diaspora.Observability/AlertCatalog.md";

    /// <summary>
    /// The sanity rule the other three rest on.
    /// <para>
    /// Every rule below is a subset assertion, and a subset assertion over an empty set passes
    /// while proving nothing — a parser that silently stopped matching would make this whole file
    /// green. The floors are deliberately below today's counts and above zero.
    /// </para>
    /// </summary>
    [Fact]
    public void The_catalogue_and_the_scan_both_found_something_to_check()
    {
        Instruments.Value.Should().HaveCountGreaterThan(40,
            "the platform declares dozens of instruments and finding a handful means the " +
            "declaration scan has stopped matching");

        AlertingInstruments.Should().HaveCountGreaterThan(9,
            "thirteen instruments documented alerting when this rule was written");

        CatalogInstruments.Should().HaveCountGreaterThan(9);
        CatalogMeters.Should().HaveCountGreaterThan(4);
    }

    /// <summary>
    /// Forward: an operator building an alert from this file must find the metric at the other end.
    /// </summary>
    [Fact]
    public void Every_instrument_the_catalogue_names_exists()
    {
        var declared = Instruments.Value.Select(instrument => instrument.Name).ToHashSet(StringComparer.Ordinal);

        CatalogInstruments.Where(name => !declared.Contains(name))
            .Should().BeEmpty(
                "{0} names these instruments and nothing in src/ creates them", CatalogPath);
    }

    /// <summary>
    /// Inverse, and the one that stops the catalogue going stale: the platform keeps deciding, in
    /// doc comments, that a counter is worth alerting on, and before this file those decisions
    /// reached only the next person to open that same file.
    /// <para>
    /// Keyed on an instrument declaration whose own documentation discusses alerting, not on the
    /// word appearing anywhere — a file that merely talks about alerting is not a finding. The
    /// catalogue has a section for the one instrument whose documentation says the opposite
    /// (<c>diaspora.automation.skipped</c>, "never alerted on as one"), because a rule keyed on
    /// prose cannot read a negation and a place to record the exception is cheaper than trying.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_instrument_documented_as_worth_alerting_on_is_in_the_catalogue()
    {
        AlertingInstruments
            .Where(instrument => !CatalogInstruments.Contains(instrument.Name))
            .Select(instrument => $"{instrument.Name} ({instrument.Path})")
            .Should().BeEmpty(
                "these instruments document themselves as worth alerting on and {0} does not " +
                "mention them, so the decision reaches only whoever next opens that source file",
                CatalogPath);
    }

    /// <summary>
    /// Upward: a meter nothing contributes is never subscribed, so its measurements are computed
    /// and dropped inside the process — BARCH-008 one meter at a time, and invisible in exactly
    /// the same way.
    /// </summary>
    [Fact]
    public void Every_meter_the_catalogue_names_is_subscribed_by_a_contributor()
    {
        var subscribed = ContributedMeters();

        subscribed.Should().HaveCountGreaterThan(9,
            "the platform has more than twenty observability contributors, and finding a few " +
            "means discovery has broken rather than that few exist");

        CatalogMeters.Where(meter => !subscribed.Contains(meter))
            .Should().BeEmpty(
                "{0} names these meters and no IObservabilityContributor subscribes them, so " +
                "nothing they measure ever leaves the process", CatalogPath);
    }

    /// <summary>
    /// The audit asked for five. Counted rather than described, because "the first five alerts"
    /// is the one claim in the document that a reader checks by counting.
    /// </summary>
    [Fact]
    public void The_catalogue_defines_exactly_five_launch_alerts()
    {
        var rows = Section("## The five for launch")
            .Where(line => line.StartsWith("| ", StringComparison.Ordinal))
            .Where(line => Regex.IsMatch(line, @"^\|\s*\d+\s*\|", RegexOptions.None, TimeSpan.FromSeconds(5)))
            .ToList();

        rows.Should().HaveCount(5, "BARCH-008 step 3 asked for the first five alerts");
    }

    /// <summary>
    /// <b>A guardrail's input must be in the repository, or the guardrail is a comment.</b>
    /// <para>
    /// <c>.gitignore</c> excludes markdown by extension, and that has now caught three build or
    /// test inputs: the two analyzer release files, which failed a clean Release build with
    /// <c>CS2001</c>; <c>AnalyzerCatalog.md</c>, whose test passed on every working tree and had
    /// never run anywhere else; and this catalogue, which was deliberately written under
    /// <c>src/</c> to avoid the <c>docs/</c> version of the trap and was caught by the extension
    /// rule regardless.
    /// </para>
    /// <para>
    /// The failure is invisible locally by construction — the file is right there — and the tests
    /// that read these do not skip when it is missing, they throw. So the check is on the ignore
    /// file itself, which is the only artefact that differs between a working tree and a clean
    /// checkout.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_markdown_file_a_guardrail_reads_is_re_included_by_gitignore()
    {
        string[] inputs =
        [
            CatalogPath,
            "src/Diaspora.Analyzers/AnalyzerCatalog.md",
            "src/Diaspora.Analyzers/AnalyzerReleases.Shipped.md",
            "src/Diaspora.Analyzers/AnalyzerReleases.Unshipped.md"
        ];

        var gitignore = File.ReadAllLines(Path.Combine(SourceTree.Root.FullName, ".gitignore"))
            .Select(line => line.Trim())
            .ToList();

        gitignore.Should().Contain(
            "*.md",
            "this rule guards against a blanket markdown exclusion swallowing a build or test " +
            "input. If that exclusion has been removed, these files need no re-inclusion and " +
            "this test should go with it rather than be weakened");

        foreach (var input in inputs)
        {
            File.Exists(Path.Combine(SourceTree.Root.FullName, input))
                .Should().BeTrue("{0} is read by a test or compiled by a project", input);

            gitignore.Should().Contain(
                "!" + input,
                "{0} is excluded by the blanket *.md rule and needs an explicit re-inclusion, or " +
                "it is absent on every clean checkout while being present on every machine that " +
                "would notice", input);
        }
    }

    // ─── Reading the catalogue ──────────────────────────────────────────────────────────────

    private static readonly Lazy<string> Catalog = new(() =>
        File.ReadAllText(Path.Combine(SourceTree.Root.FullName, CatalogPath))
            .ReplaceLineEndings("\n"));

    /// <summary>
    /// Backticked tokens inside table rows, split by the casing the platform already uses: a meter
    /// is <c>Diaspora.Something</c> and an instrument is <c>diaspora.something</c>. Reading only
    /// table rows keeps the prose free to mention either without becoming an assertion.
    /// </summary>
    private static readonly Regex Token =
        new(@"`([A-Za-z][A-Za-z0-9_.]*)`", RegexOptions.Compiled, TimeSpan.FromSeconds(5));

    private static HashSet<string> TableTokens(Func<string, bool> accept) =>
        Catalog.Value.Split('\n')
            .Where(line => line.StartsWith("| ", StringComparison.Ordinal))
            .SelectMany(line => Token.Matches(line).Select(match => match.Groups[1].Value))
            .Where(accept)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> CatalogInstruments =>
        TableTokens(token => token.StartsWith("diaspora.", StringComparison.Ordinal));

    private static HashSet<string> CatalogMeters =>
        TableTokens(token => token.StartsWith("Diaspora.", StringComparison.Ordinal));

    private static IEnumerable<string> Section(string heading)
    {
        var lines = Catalog.Value.Split('\n');
        var start = Array.FindIndex(lines, line => line.StartsWith(heading, StringComparison.Ordinal));

        start.Should().BeGreaterThanOrEqualTo(0, "{0} must contain the heading '{1}'", CatalogPath, heading);

        return lines.Skip(start + 1)
            .TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal));
    }

    // ─── Reading the source ─────────────────────────────────────────────────────────────────

    private sealed record DeclaredInstrument(string Name, string Meter, string Path, bool MentionsAlerting);

    private static IEnumerable<DeclaredInstrument> AlertingInstruments =>
        Instruments.Value.Where(instrument => instrument.MentionsAlerting);

    private static readonly Lazy<IReadOnlyList<DeclaredInstrument>> Instruments = new(Discover);

    /// <summary>
    /// Every <c>Meter.Create…</c> in the platform, with the meter it belongs to and whether its
    /// own documentation discusses alerting.
    /// <para>
    /// Roslyn rather than a text search, so the doc comment is read from the declaration it is
    /// attached to. A regex that looked backwards a fixed number of lines attributes one
    /// instrument's documentation to the next one along the moment somebody reformats.
    /// </para>
    /// </summary>
    private static List<DeclaredInstrument> Discover()
    {
        var instruments = new List<DeclaredInstrument>();

        foreach (var file in SourceTree.ProductionFiles)
        {
            var meter = MeterNameIn(file);

            foreach (var member in file.Root.DescendantNodes().OfType<MemberDeclarationSyntax>())
            {
                var name = InstrumentNameOf(member);

                if (name is null)
                {
                    continue;
                }

                var documentation = member.GetLeadingTrivia()
                    .Select(trivia => trivia.GetStructure())
                    .OfType<DocumentationCommentTriviaSyntax>()
                    .FirstOrDefault()
                    ?.ToFullString() ?? string.Empty;

                instruments.Add(new DeclaredInstrument(
                    name,
                    meter ?? "(none)",
                    file.Path,
                    documentation.Contains("alert", StringComparison.OrdinalIgnoreCase)));
            }
        }

        return instruments;
    }

    /// <summary>
    /// The first string literal passed to a <c>Create…</c> call in the member's initialiser.
    /// <para>
    /// <b>Fields and properties both</b>, because the platform declares instruments as both —
    /// <c>public static readonly Counter&lt;long&gt; X = …</c> in most modules and
    /// <c>public static Counter&lt;long&gt; X { get; } = …</c> in Integrations. Walking only
    /// fields misses the second form, and the rule that would have caught it is the inverse one,
    /// which cannot: an instrument the scan does not see produces no requirement at all, so the
    /// scanner fails open and a catalogue missing those entries looks complete.
    /// </para>
    /// </summary>
    private static string? InstrumentNameOf(MemberDeclarationSyntax member) =>
        Initialisers(member)
            .SelectMany(value => value.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
            .Where(invocation => invocation.Expression is MemberAccessExpressionSyntax access
                && access.Name is GenericNameSyntax generic
                && generic.Identifier.ValueText.StartsWith("Create", StringComparison.Ordinal)
                && generic.Identifier.ValueText.Length > "Create".Length)
            .SelectMany(invocation => invocation.ArgumentList.Arguments)
            .Select(argument => argument.Expression)
            .OfType<LiteralExpressionSyntax>()
            .Where(literal => literal.IsKind(SyntaxKind.StringLiteralExpression))
            .Select(literal => literal.Token.ValueText)
            .FirstOrDefault();

    /// <summary>Whatever a member is assigned, whichever of the two forms declares it.</summary>
    private static IEnumerable<ExpressionSyntax> Initialisers(MemberDeclarationSyntax member) =>
        member switch
        {
            FieldDeclarationSyntax field => field.Declaration.Variables
                .Select(variable => variable.Initializer?.Value)
                .OfType<ExpressionSyntax>(),

            PropertyDeclarationSyntax property when property.Initializer is not null =>
                [property.Initializer.Value],

            _ => []
        };

    /// <summary>The <c>public const string SectionName</c> equivalent for telemetry classes.</summary>
    private static string? MeterNameIn(SourceFile file) =>
        file.Root.DescendantNodes().OfType<FieldDeclarationSyntax>()
            .Where(field => field.Modifiers.Any(SyntaxKind.ConstKeyword))
            .SelectMany(field => field.Declaration.Variables)
            .Where(variable => variable.Identifier.ValueText == "SourceName")
            .Select(variable => variable.Initializer?.Value)
            .OfType<LiteralExpressionSyntax>()
            .Select(literal => literal.Token.ValueText)
            .FirstOrDefault();

    /// <summary>
    /// Every meter name an <see cref="IObservabilityContributor"/> asks the host to subscribe,
    /// read by constructing the contributors the kernel would construct.
    /// </summary>
    private static HashSet<string> ContributedMeters()
    {
        var meters = new HashSet<string>(StringComparer.Ordinal);

        var contributors = ModuleAssemblyScanner.FindImplementations<IObservabilityContributor>(
            ModuleAssemblyScanner.FindPlatformAssemblies());

        foreach (var type in contributors)
        {
            if (type.GetConstructor(Type.EmptyTypes) is null)
            {
                continue;
            }

            var contributor = (IObservabilityContributor)Activator.CreateInstance(type)!;

            foreach (var meter in contributor.Meters)
            {
                meters.Add(meter);
            }
        }

        return meters;
    }
}
