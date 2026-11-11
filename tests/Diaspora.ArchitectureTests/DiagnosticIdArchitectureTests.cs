using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// One <c>DSP</c> id means one thing.
/// <para>
/// <b>The platform spends its id space on two unrelated mechanisms.</b> Most ids belong to analyzer
/// rules in <c>Diaspora.Analyzers</c> and are declared in <c>AnalyzerReleases.Unshipped.md</c>.
/// A handful belong to no analyzer at all: they are the <c>DiagnosticId</c> on an
/// <c>[Obsolete(error: true)]</c> attribute carried by a retired type, which the <i>compiler</i>
/// reports. The analyzer catalogue states the rule both obey — ids are permanent, a retired rule
/// keeps its number — but nothing enforced it, because the two live in different files and neither
/// mechanism can see the other.
/// </para>
/// <para>
/// <b>This rule exists because the two collided (11 November 2026).</b> PSR-1 Final Certification
/// reserved <c>DSP4005</c> for seven retired <c>SharedKernel</c> primitives; BSOLID-001 then gave
/// the same id to a new analyzer rule. Both shipped. Nothing failed, because a collision is not a
/// compile error, not a test failure and not visible in either file on its own.
/// </para>
/// <para>
/// <b>What a collision costs is a suppression that hits the wrong target.</b>
/// <c>#pragma warning disable DSP4005</c>, or a <c>NoWarn</c> entry, silences everything wearing
/// that id. A suppression written against a retired <c>OperationResult</c> would have switched off
/// "a request must declare what it requires of its caller" — the guardrail BSEC-003 was closed
/// with — years later and in a file with no apparent connection to authorization. That failure is
/// silent in both directions, which is why it needs a rule rather than a convention.
/// </para>
/// </summary>
public sealed class DiagnosticIdArchitectureTests
{
    private const string Releases = "src/Diaspora.Analyzers/AnalyzerReleases.Unshipped.md";

    /// <summary>
    /// <b>No id is both an analyzer rule and a compiler obsoletion.</b>
    /// </summary>
    /// <remarks>
    /// Both sides are read from the source rather than from a list kept here, so the rule cannot
    /// go stale the way the catalogue did: adding a rule to the releases file or an
    /// <c>[Obsolete]</c> to a type is enough to be counted.
    /// </remarks>
    [Fact]
    public void No_diagnostic_id_is_both_an_analyzer_rule_and_a_compiler_obsoletion()
    {
        var rules = AnalyzerRuleIds();
        var obsoletions = ObsoletionIds();

        rules.Should().NotBeEmpty("the releases file is this rule's left-hand side");
        obsoletions.Should().NotBeEmpty("the retired types are this rule's right-hand side");

        rules.Intersect(obsoletions, StringComparer.Ordinal).Should().BeEmpty(
            "an id carried by both is one suppression away from switching off the wrong thing, "
            + "and the id space is permanent precisely so that cannot happen");
    }

    /// <summary>Every rule id the analyzer pack declares.</summary>
    private static List<string> AnalyzerRuleIds()
    {
        var path = Path.Combine(SourceTree.Root.FullName, Releases);

        return
        [
            .. File.ReadAllLines(path)
                .Where(line => line.StartsWith("DSP", StringComparison.Ordinal))
                .Select(line => line.Split('|')[0].Trim())
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
        ];
    }

    /// <summary>
    /// Every id reserved by an <c>[Obsolete]</c> attribute under <c>src/</c>.
    /// </summary>
    /// <remarks>
    /// Syntax rather than reflection, because a type retired with <c>error: true</c> cannot be
    /// loaded or named from a test — referring to it is the compile error the attribute exists to
    /// produce. The attribute's own text is the only place left to read it from.
    /// </remarks>
    private static List<string> ObsoletionIds()
    {
        var found = new List<string>();

        foreach (var file in SourceTree.ProductionFiles)
        {
            foreach (var attribute in file.Root.DescendantNodes().OfType<AttributeSyntax>())
            {
                var name = attribute.Name.ToString();

                if (name is not ("Obsolete" or "ObsoleteAttribute")
                    && !name.EndsWith(".Obsolete", StringComparison.Ordinal)
                    && !name.EndsWith(".ObsoleteAttribute", StringComparison.Ordinal))
                {
                    continue;
                }

                var argument = attribute.ArgumentList?.Arguments
                    .FirstOrDefault(candidate =>
                        candidate.NameEquals?.Name.Identifier.ValueText == "DiagnosticId");

                if (argument?.Expression is LiteralExpressionSyntax literal
                    && literal.Token.ValueText.StartsWith("DSP", StringComparison.Ordinal))
                {
                    found.Add(literal.Token.ValueText);
                }
            }
        }

        return [.. found.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }
}
