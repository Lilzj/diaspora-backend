using Diaspora.Platform.Validation;
using FluentAssertions;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// B1.4's own scaffolding, checked the way it checks everything else.
/// <para>
/// <b>A guardrail phase can fail in one uniquely embarrassing way: by shipping guardrails nothing
/// runs.</b> An analyzer pack referenced by one project, a validator nobody discovers, a diagnostic
/// with no catalogue entry — each of those leaves a repository that looks governed and is not.
/// Every test here asks whether the enforcement is actually connected to the thing it enforces.
/// </para>
/// </summary>
public sealed class EngineeringGuardrailArchitectureTests
{
    /// <summary>
    /// <b>The analyzer pack is handed to every compilation, from one place.</b> A per-project
    /// reference would be twenty-two places to forget, and the project that forgot would be exempt
    /// from every rule silently — which is the failure mode the platform already recorded when a
    /// model-reading test hard-coded seven contributors and stayed at seven for two more verticals.
    /// </summary>
    [Fact]
    public void Every_project_compiles_against_the_analyzer_pack()
    {
        var props = File.ReadAllText(Path.Combine(SourceTree.Root.FullName, "Directory.Build.props"));

        props.Should().Contain(
            "src\\Diaspora.Analyzers\\Diaspora.Analyzers.csproj",
            "the pack is wired in once, at the repository root, so no project can be missed");

        props.Should().Contain(
            "OutputItemType=\"Analyzer\"",
            "without this the reference is an ordinary library and nothing runs");

        props.Should().Contain(
            "ReferenceOutputAssembly=\"false\"",
            "nothing in the platform should be able to call the analyzers");
    }

    /// <summary>
    /// The two exemptions are structural — an analyzer cannot analyze itself, and its test project
    /// references it as a library — and there must be exactly two. A third would be a project
    /// quietly opting out of the whole pack.
    /// </summary>
    [Fact]
    public void Exactly_two_projects_opt_out_of_the_analyzer_pack()
    {
        var optedOut = Directory
            .EnumerateFiles(SourceTree.Root.FullName, "*.csproj", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "<UseDiasporaAnalyzers>false</UseDiasporaAnalyzers>", StringComparison.Ordinal))
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();

        optedOut.Should().BeEquivalentTo(
            ["Diaspora.AnalyzerTests"],
            "the analyzer project is excluded by name in Directory.Build.props and needs no switch; " +
            "its test project is the only opt-out, and any other is a project exempting itself");
    }

    /// <summary>
    /// Every rule the pack can report is recorded in the release-tracking file the compiler reads
    /// and in the catalogue a human reads. <c>RS2008</c> already fails the build on the first;
    /// nothing but this fails it on the second.
    /// <para>
    /// <b>No longer a <c>[DocumentationFact]</c> (11 November 2026).</b> It was gated on
    /// <c>docs/platform</c> existing, so it skipped on every clean checkout and in CI: the rule
    /// that catches an undocumented rule was run only by whoever already had the documentation,
    /// and DSP4005 duly shipped with no entry. Both files it reads are committed now, so it runs
    /// everywhere and the gate would be a lie.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_analyzer_diagnostic_is_catalogued()
    {
        var ids = AnalyzerIds();

        ids.Should().HaveCountGreaterThanOrEqualTo(15, "the phase shipped a pack, not a rule");

        var catalogue = File.ReadAllText(Path.Combine(
            SourceTree.Root.FullName, "src", "Diaspora.Analyzers", "AnalyzerCatalog.md"));

        var release = File.ReadAllText(Path.Combine(
            SourceTree.Root.FullName, "src", "Diaspora.Analyzers", "AnalyzerReleases.Unshipped.md"));

        foreach (var id in ids)
        {
            catalogue.Should().Contain(id, "a build log's four-character code has to be lookupable");
            release.Should().Contain(id, "release tracking is what stops an id being reused");
        }
    }

    /// <summary>
    /// <b>Every suppression is written down.</b> A rule switched off in an <c>.editorconfig</c> with
    /// no reason beside it is indistinguishable from a rule nobody meant to have, and the person who
    /// finds it two years later cannot tell which. Each entry carries its reason and the condition
    /// that would remove it; the guidelines document explains what qualifies.
    /// </summary>
    [DocumentationFact]
    public void Every_suppressed_rule_carries_its_reason()
    {
        var lines = File.ReadAllLines(Path.Combine(SourceTree.Root.FullName, ".editorconfig"));

        var suppressions = lines
            .Select((text, index) => (Text: text.Trim(), Index: index))
            .Where(line =>
                line.Text.StartsWith("dotnet_diagnostic.DSP", StringComparison.Ordinal) &&
                line.Text.EndsWith("severity = none", StringComparison.Ordinal))
            .ToList();

        foreach (var (text, index) in suppressions)
        {
            // The comment block above it. Twenty lines is generous on purpose: the reason should be
            // a paragraph, not a word, and a short one is as suspicious as an absent one.
            var preceding = lines.Take(index).Reverse().Take(20);

            preceding.Should().Contain(
                line => line.TrimStart().StartsWith('#'),
                "'{0}' switches a platform rule off and must say why, and what would bring it back",
                text);
        }

        File.Exists(Path.Combine(
                SourceTree.Root.FullName, "docs", "platform", "b1.4-suppression-guidelines.md"))
            .Should().BeTrue("the process for adding one has to be written down somewhere");
    }

    /// <summary>
    /// The boot-time validators are discovered, not listed — so the way they can disappear is a
    /// namespace typo or a deleted file, neither of which any other test would notice. The registry
    /// names them; this asserts the named ones exist, implement the seam, and are constructible.
    /// </summary>
    [Fact]
    public void Every_kernel_validator_ships_and_implements_the_seam()
    {
        var validators = PlatformStartupValidationRegistration.KernelValidators;

        validators.Should().HaveCountGreaterThanOrEqualTo(6);

        foreach (var validator in validators)
        {
            validator.Should().BeAssignableTo<IStartupValidator>();

            validator.IsAbstract.Should().BeFalse();
            validator.IsSealed.Should().BeTrue(
                "{0} is a check, not an extension point — the extension point is IStartupValidator",
                validator.Name);
        }
    }

    /// <summary>
    /// The validator seam is discovered across every platform assembly, so a module may add its own.
    /// This asserts the discovery finds the kernel's, which is the cheapest way to notice that the
    /// scan has stopped finding anything at all.
    /// </summary>
    [Fact]
    public void The_validator_seam_is_discoverable_the_way_the_kernel_discovers_it()
    {
        var discovered = Platform.Modules.ModuleAssemblyScanner
            .FindImplementations<IStartupValidator>(
                Platform.Modules.ModuleAssemblyScanner.FindPlatformAssemblies());

        discovered.Should().Contain(PlatformStartupValidationRegistration.KernelValidators);
    }

    /// <summary>
    /// The documents the phase promised. Named individually rather than counted, because a
    /// count passes when the wrong ten files exist.
    /// <para>
    /// Ten, not eleven: <c>b1.4-analyzer-catalog.md</c> left this directory on 11 November 2026
    /// and is now <c>src/Diaspora.Analyzers/AnalyzerCatalog.md</c>, committed, because a test
    /// reads it. This theory still guards the documentation that only a human reads.
    /// </para>
    /// </summary>
    [DocumentationTheory]
    [InlineData("b1.4-engineering-guardrails-guide.md")]
    [InlineData("b1.4-contributor-validation-guide.md")]
    [InlineData("b1.4-domain-event-safety-guide.md")]
    [InlineData("b1.4-messaging-guardrails-guide.md")]
    [InlineData("b1.4-cqrs-guardrails-guide.md")]
    [InlineData("b1.4-startup-validation-guide.md")]
    [InlineData("b1.4-architecture-rule-catalog.md")]
    [InlineData("b1.4-suppression-guidelines.md")]
    [InlineData("b1.4-readiness-report.md")]
    [InlineData("b1.4-platform-evolution-report.md")]
    public void The_phase_documentation_exists(string document)
    {
        var path = Path.Combine(SourceTree.Root.FullName, "docs", "platform", document);

        File.Exists(path).Should().BeTrue("{0} is a deliverable of this phase", document);

        new FileInfo(path).Length.Should().BeGreaterThan(
            1_000, "{0} exists but says nothing", document);
    }

    /// <summary>
    /// Every diagnostic id the pack declares, read from the release-tracking file.
    /// <para>
    /// Read rather than reflected on purpose. The analyzer assembly is handed to the compiler and
    /// deliberately kept out of every project's reference set, so it is not loadable from here — and
    /// the release file is not a second source of truth anyway: <c>RS2008</c> fails the analyzer's
    /// own build if it disagrees with the descriptors, and <c>Diaspora.AnalyzerTests</c> asserts the
    /// severities match. What is unchecked without this test is the third leg, the catalogue a human
    /// reads.
    /// </para>
    /// </summary>
    private static IReadOnlyList<string> AnalyzerIds()
    {
        var path = Path.Combine(
            SourceTree.Root.FullName, "src", "Diaspora.Analyzers", "AnalyzerReleases.Unshipped.md");

        return [.. File.ReadAllLines(path)
            .Where(line => line.StartsWith("DSP", StringComparison.Ordinal))
            .Select(line => line.Split('|')[0].Trim())
            .OrderBy(id => id, StringComparer.Ordinal)];
    }
}
