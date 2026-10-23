using FluentAssertions;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The configuration reference, kept true by construction.
/// <para>
/// <b>This is DEBT-070 closed rather than described.</b> PSR-1.1 found that thirty-odd configuration
/// sections existed with no single place to read them, and said what the right fix looked like:
/// generated, not written, <em>"because a hand-written one goes stale on the first new setting."</em>
/// The generator is <see cref="ConfigurationReference"/>; these rules are what make it binding.
/// </para>
/// <para>
/// <b>Two rules rather than one, because they fail for different reasons and a reader deserves to
/// know which.</b> The first says the document is byte-for-byte what the source produces. The second
/// says every section is reachable from the index — a cheap check that would catch a generator
/// change that quietly dropped a section from the table of contents while still emitting its body.
/// </para>
/// </summary>
public sealed class ConfigurationReferenceArchitectureTests
{
    /// <summary>
    /// <b>The reference matches the options types.</b>
    /// <para>
    /// A setting added, renamed, retyped, re-defaulted or re-documented fails this rule, and the
    /// failure names the one command that fixes it. That is the whole mechanism: the document cannot
    /// drift, because drifting is a red build.
    /// </para>
    /// </summary>
    [DocumentationFact]
    public void The_configuration_reference_is_current()
    {
        var expected = ConfigurationReference.Render();
        var path = Path.Combine(
            SourceTree.Root.FullName, ConfigurationReference.DocumentPath);

        if (ConfigurationReference.RegenerationRequested)
        {
            File.WriteAllText(path, expected);
        }

        File.Exists(path).Should().BeTrue(
            $"{ConfigurationReference.DocumentPath} is the platform's only complete answer to "
            + "\"what can I set\"");

        var actual = File.ReadAllText(path).ReplaceLineEndings("\n");

        actual.Should().Be(
            expected,
            $"the configuration reference is generated from the options types. Regenerate it with "
            + $"{ConfigurationReference.RegenerateVariable}=1 dotnet test "
            + "tests/Diaspora.ArchitectureTests --filter "
            + "FullyQualifiedName~ConfigurationReference, then review and commit the result");
    }

    /// <summary>
    /// <b>Every section the platform declares appears in the reference's index.</b>
    /// <para>
    /// Independent of the rule above on purpose: that one compares two strings the same code
    /// produced, so a generator that stopped emitting the index would still satisfy it. This one
    /// asks the question an operator asks.
    /// </para>
    /// </summary>
    [DocumentationFact]
    public void Every_configuration_section_is_indexed()
    {
        var document = File.ReadAllText(
            Path.Combine(SourceTree.Root.FullName, ConfigurationReference.DocumentPath));

        ConfigurationReference.Sections.Should().NotBeEmpty(
            "the platform binds configuration through options types that declare a SectionName, "
            + "and finding none means the discovery has broken rather than that none exist");

        foreach (var section in ConfigurationReference.Sections)
        {
            document.Should().Contain(
                $"| [`{section.Name}`]",
                $"an operator who cannot find {section.Name} in the index cannot configure it");
        }
    }
}
