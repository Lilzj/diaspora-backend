using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The platform's configuration surface, derived from the options types themselves.
/// <para>
/// <b>DEBT-070's answer, and the reason it is generated rather than written.</b> PSR-1.1 recorded
/// that an operator deploying this platform could not answer "what can I set" without reading
/// fourteen assemblies: thirty-odd sections, most of which never appear in <c>appsettings.json</c>
/// and exist only as compiled-in defaults, each documented in its own options type and its own
/// module's guide and nowhere together. Its decision was explicit — <em>"generating it from the
/// options types is the version worth building, because a hand-written one goes stale on the first
/// new setting."</em>
/// </para>
/// <para>
/// Roslyn rather than reflection, for two reasons. The solution produces no XML documentation
/// files, so reflection cannot reach the summaries that make the reference worth reading. And the
/// default as <em>written</em> — <c>TimeSpan.FromMinutes(15)</c>, <c>[]</c>, <c>"en"</c> — says more
/// to an operator than the runtime value a constructed instance would report.
/// </para>
/// </summary>
internal static class ConfigurationReference
{
    /// <summary>Where the generated reference lives.</summary>
    public const string DocumentPath = "docs/platform/configuration-reference.md";

    /// <summary>
    /// Set <c>DIASPORA_REGENERATE_DOCS=1</c> to rewrite the reference in place instead of failing.
    /// <para>
    /// The approval-test shape: a normal run — a developer's, and CI's — never writes to the
    /// repository, and the one command that does says so in its name.
    /// </para>
    /// </summary>
    public const string RegenerateVariable = "DIASPORA_REGENERATE_DOCS";

    /// <summary>Every configuration section the platform declares, with its settings.</summary>
    public static IReadOnlyList<ConfigurationSection> Sections { get; } = Discover();

    public static bool RegenerationRequested =>
        Environment.GetEnvironmentVariable(RegenerateVariable) == "1";

    private static List<ConfigurationSection> Discover()
    {
        var sections = new List<ConfigurationSection>();

        foreach (var file in SourceTree.ProductionFiles)
        {
            foreach (var type in file.Root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var section = SectionNameOf(type);

                if (section is null)
                {
                    continue;
                }

                sections.Add(new ConfigurationSection(
                    section,
                    type.Identifier.ValueText,
                    file.Path,
                    Summary(type),
                    SettingsOf(type)));
            }
        }

        return sections
            .OrderBy(section => section.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The <c>public const string SectionName = "…"</c> every options type on this platform
    /// declares. A type without one is not bound to configuration and is not part of this surface.
    /// </summary>
    private static string? SectionNameOf(TypeDeclarationSyntax type) =>
        type.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(field => field.Modifiers.Any(SyntaxKind.ConstKeyword))
            .SelectMany(field => field.Declaration.Variables)
            .Where(variable => variable.Identifier.ValueText == "SectionName")
            .Select(variable => variable.Initializer?.Value)
            .OfType<LiteralExpressionSyntax>()
            .Select(literal => literal.Token.ValueText)
            .FirstOrDefault();

    private static List<ConfigurationSetting> SettingsOf(TypeDeclarationSyntax type) =>
        type.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(property => property.Modifiers.Any(SyntaxKind.PublicKeyword))
            .Where(property => property.AccessorList is not null)
            .Where(property => property.AccessorList!.Accessors.Any(accessor =>
                accessor.IsKind(SyntaxKind.SetAccessorDeclaration)
                || accessor.IsKind(SyntaxKind.InitAccessorDeclaration)))
            .Select(property => new ConfigurationSetting(
                property.Identifier.ValueText,
                property.Type.ToString(),
                property.Initializer?.Value.ToString().Trim(),
                Summary(property)))
            .ToList();

    /// <summary>
    /// The first sentence of the member's <c>&lt;summary&gt;</c>, flattened to one line.
    /// <para>
    /// First sentence only, deliberately. Several of these summaries run to three paragraphs
    /// explaining why a default is what it is — worth reading in the source, and not what a table
    /// of two hundred and eighty rows is for.
    /// </para>
    /// </summary>
    private static string Summary(SyntaxNode member)
    {
        var comment = member.GetLeadingTrivia()
            .Select(trivia => trivia.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .FirstOrDefault();

        var summary = comment?.Content
            .OfType<XmlElementSyntax>()
            .FirstOrDefault(element =>
                element.StartTag.Name.LocalName.ValueText == "summary");

        if (summary is null)
        {
            return string.Empty;
        }

        var text = string.Concat(summary.Content
            .OfType<XmlTextSyntax>()
            .SelectMany(node => node.TextTokens)
            .Select(token => token.ValueText));

        text = string.Join(' ', text.Split(
            ['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        // A <para> or a <b> ends the flattened run; the first sentence is what fits a table cell.
        var stop = text.IndexOf(". ", StringComparison.Ordinal);

        if (stop > 0)
        {
            text = text[..(stop + 1)];
        }

        return text.Replace("|", "\\|", StringComparison.Ordinal).Trim();
    }

    /// <summary>Renders the whole reference.</summary>
    public static string Render()
    {
        var settings = Sections.Sum(section => section.Settings.Count);

        var markdown = new StringBuilder();

        markdown.AppendLine("# Configuration Reference");
        markdown.AppendLine();
        markdown.AppendLine("**Generated.** Do not edit by hand.");
        markdown.AppendLine();
        markdown.AppendLine(
            "Produced from the options types by `ConfigurationReferenceArchitectureTests`, which");
        markdown.AppendLine(
            "fails when this file and the source disagree. Regenerate with:");
        markdown.AppendLine();
        markdown.AppendLine("```bash");
        markdown.AppendLine(
            "DIASPORA_REGENERATE_DOCS=1 dotnet test tests/Diaspora.ArchitectureTests "
            + "--filter FullyQualifiedName~ConfigurationReference");
        markdown.AppendLine("```");
        markdown.AppendLine();
        markdown.AppendLine(CultureInfo.InvariantCulture,
            $"**{Sections.Count} sections, {settings} settings.**");

        markdown.AppendLine("""

            Every setting here has a compiled-in default and is optional unless its description says
            otherwise. Most never appear in `appsettings.json` — which is exactly the problem this
            document exists to solve, recorded as **DEBT-070**: before it, answering "what can I set"
            meant reading fourteen assemblies.

            Nesting is expressed with `:` — a setting shown under `Identity:Jwt` is
            `Identity.Jwt` in JSON, nested two levels deep, and `Identity__Jwt__…` as an environment
            variable.

            ---

            """);

        markdown.AppendLine("## Sections");
        markdown.AppendLine();
        markdown.AppendLine("| Section | Options type | Settings |");
        markdown.AppendLine("|---|---|---:|");

        foreach (var section in Sections)
        {
            markdown.AppendLine(CultureInfo.InvariantCulture,
                $"| [`{section.Name}`](#{Anchor(section.Name)}) | `{section.TypeName}` "
                + $"| {section.Settings.Count} |");
        }

        markdown.AppendLine();
        markdown.AppendLine("---");

        foreach (var section in Sections)
        {
            markdown.AppendLine();
            markdown.AppendLine(CultureInfo.InvariantCulture, $"## `{section.Name}`");
            markdown.AppendLine();
            markdown.AppendLine(CultureInfo.InvariantCulture,
                $"`{section.TypeName}` — [{section.SourcePath}](../../{section.SourcePath})");

            if (!string.IsNullOrWhiteSpace(section.Summary))
            {
                markdown.AppendLine();
                markdown.AppendLine(section.Summary);
            }

            markdown.AppendLine();

            if (section.Settings.Count == 0)
            {
                markdown.AppendLine("*No settable properties.*");

                continue;
            }

            markdown.AppendLine("| Setting | Type | Default | Description |");
            markdown.AppendLine("|---|---|---|---|");

            foreach (var setting in section.Settings)
            {
                var @default = setting.Default is null
                    ? "—"
                    : $"`{setting.Default}`";

                markdown.AppendLine(CultureInfo.InvariantCulture,
                    $"| `{setting.Name}` | `{setting.Type}` | {@default} | {setting.Summary} |");
            }
        }

        return markdown.ToString().ReplaceLineEndings("\n");
    }

    /// <summary>GitHub's heading anchor for a section title of the form <c>`Name`</c>.</summary>
    private static string Anchor(string section) =>
        section.ToLowerInvariant().Replace(':', '-');
}

/// <summary>One configuration section, as declared by its options type.</summary>
internal sealed record ConfigurationSection(
    string Name,
    string TypeName,
    string SourcePath,
    string Summary,
    IReadOnlyList<ConfigurationSetting> Settings);

/// <summary>One setting within a section.</summary>
internal sealed record ConfigurationSetting(
    string Name,
    string Type,
    string? Default,
    string Summary);
