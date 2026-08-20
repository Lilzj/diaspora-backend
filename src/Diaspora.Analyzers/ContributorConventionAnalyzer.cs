using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Diaspora.Analyzers;

/// <summary>
/// The naming and shape rules of B2.6's contributor model.
/// <para>
/// <b>Names are checked where they are declared, not where they are written.</b> A module names
/// its gates and permissions in a <c>static class</c> of constants and then hands those constants
/// to a <see cref="Rules.FeatureGateName">definition</see> — so the analyzer resolves the constant
/// through the semantic model rather than insisting on a literal. That is what makes the rule
/// apply to the platform as it is actually written, instead of to a style nobody uses.
/// </para>
/// <para>
/// <b>Two vocabularies, kept visibly apart.</b> Permissions use colons and policies use dots, and
/// the platform has been explicit since B2 that this is deliberate: a reader must be able to tell
/// from the string alone whether they are looking at what an endpoint asks for or at what a caller
/// holds. A rule is the only thing that keeps a distinction like that alive past its author.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ContributorConventionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The words the platform has more than one of. Each key is a synonym somebody reached for;
    /// each value is the word the catalogue already uses for the same idea.
    /// </summary>
    private static readonly Dictionary<string, string> PreferredActions = new(StringComparer.Ordinal)
    {
        ["view"] = "read",
        ["browse"] = "read",
        ["feed"] = "read",
        ["list"] = "read",
        ["get"] = "read",
        ["fetch"] = "read",
        ["retrieve"] = "read",
        ["add"] = "create",
        ["new"] = "create",
        ["destroy"] = "delete",
        ["erase"] = "delete"
    };

    /// <summary>
    /// The seams the kernel builds with <c>Activator.CreateInstance</c> before the container
    /// exists. Only these have to be parameterless: the two dozen reference-data contributors a
    /// vertical declares are resolved from DI like anything else, and one of them — Guides'
    /// projected country context — legitimately takes a repository.
    /// </summary>
    private static readonly string[] KernelConstructedSeams =
    [
        "IPermissionContributor", "IFeatureContributor", "IPolicyContributor",
        "IObservabilityContributor", "IEndpointContributor", "IDatabaseContributor"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [
        Rules.FeatureGateName,
        Rules.PermissionKeyName,
        Rules.PolicyName,
        Rules.PermissionVocabulary,
        Rules.ContributorShape,
        Rules.CatalogueNameCasing
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var symbols = PlatformSymbols.For(context.Compilation);

        if (symbols.FeatureDefinition is not null ||
            symbols.PermissionDefinition is not null ||
            symbols.PolicyDefinition is not null)
        {
            // Both kinds of `new`. Every contributor in the platform today writes the type out —
            // `new PermissionDefinition("…", …)` — so an analyzer that only handled that form
            // passed on the whole codebase while being blind to `new("…", …)`, which is what
            // somebody adding the next one is as likely to write.
            context.RegisterSyntaxNodeAction(
                node => AnalyzeDeclaration(node, symbols),
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression);

            context.RegisterSyntaxNodeAction(
                node => AnalyzeFactory(node, symbols), SyntaxKind.InvocationExpression);
        }

        context.RegisterSymbolAction(AnalyzeContributorShape, SymbolKind.NamedType);
    }

    /// <summary><c>new PermissionDefinition("…", …)</c> and its two siblings.</summary>
    private static void AnalyzeDeclaration(SyntaxNodeAnalysisContext context, PlatformSymbols symbols)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;

        if (creation.ArgumentList?.Arguments.FirstOrDefault()?.Expression is not { } first)
        {
            return;
        }

        var created = context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type;

        if (SymbolEqualityComparer.Default.Equals(created, symbols.FeatureDefinition))
        {
            CheckDotted(context, first, Rules.FeatureGateName);
        }
        else if (SymbolEqualityComparer.Default.Equals(created, symbols.PermissionDefinition))
        {
            CheckPermission(context, first);
        }
        else if (SymbolEqualityComparer.Default.Equals(created, symbols.PolicyDefinition))
        {
            CheckDotted(context, first, Rules.PolicyName);
        }
    }

    /// <summary><c>PolicyDefinition.ForPermission("…", …)</c> and <c>ForTier</c>.</summary>
    private static void AnalyzeFactory(SyntaxNodeAnalysisContext context, PlatformSymbols symbols)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: "ForPermission" or "ForTier"
            } ||
            invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not { } first)
        {
            return;
        }

        var target = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;

        if (SymbolEqualityComparer.Default.Equals(target?.ContainingType, symbols.PolicyDefinition))
        {
            CheckDotted(context, first, Rules.PolicyName);
        }
    }

    private static void CheckDotted(
        SyntaxNodeAnalysisContext context, ExpressionSyntax expression, DiagnosticDescriptor rule)
    {
        if (Value(context, expression) is not { } name)
        {
            return;
        }

        if (!IsNamespacedName(name, '.'))
        {
            context.ReportDiagnostic(Diagnostic.Create(rule, expression.GetLocation(), name));
        }

        ReportCasing(context, expression, name);
    }

    private static void CheckPermission(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        if (Value(context, expression) is not { } key)
        {
            return;
        }

        if (!IsNamespacedName(key, ':'))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.PermissionKeyName, expression.GetLocation(), key));

            return;
        }

        ReportCasing(context, expression, key);

        var action = key.Substring(key.LastIndexOf(':') + 1);

        // The qualifier carries meaning the vocabulary does not: 'read-any' is a different
        // authority from 'read', and neither is a synonym anybody reached for by accident.
        var head = action.Split('-')[0];

        if (PreferredActions.TryGetValue(head, out var preferred))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.PermissionVocabulary, expression.GetLocation(), key, head, preferred));
        }
    }

    /// <summary>
    /// <b>DSP5005.</b> A contributor the kernel cannot build, or one pretending to be an extension
    /// point.
    /// </summary>
    private static void AnalyzeContributorShape(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.IsStatic)
        {
            return;
        }

        // Test doubles are hand-built by the test that needs them, and the kernel refuses to scan
        // an assembly whose name ends in Tests — ModuleAssemblyScanner has excluded them since a
        // phantom test contributor collided with a real one in the integration suite. A rule about
        // what the kernel can construct has nothing to say about the assemblies it never looks at.
        if (context.Compilation.AssemblyName?.EndsWith("Tests", StringComparison.Ordinal) == true)
        {
            return;
        }

        var seams = type.AllInterfaces.Where(IsContributorSeam).ToList();

        if (seams.Count == 0)
        {
            return;
        }

        var problems = new List<string>();

        if (!type.IsSealed)
        {
            problems.Add("is not sealed, which makes a declaration look like an extension point");
        }

        var kernelConstructed = seams.Any(seam =>
            Array.IndexOf(KernelConstructedSeams, seam.Name) >= 0);

        if (kernelConstructed &&
            !type.InstanceConstructors.Any(ctor =>
                ctor.Parameters.Length == 0 && ctor.DeclaredAccessibility == Accessibility.Public))
        {
            problems.Add(
                "has no public parameterless constructor, so the kernel cannot build it before the " +
                "container exists");
        }

        foreach (var problem in problems)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.ContributorShape, type.Locations.FirstOrDefault(), type.Name, problem));
        }
    }

    /// <summary>
    /// Any of the platform's contributor seams — the five in the kernel and the two dozen a
    /// vertical declares for its own reference data. Matched by name because a rule that listed
    /// them would be out of date on the day the next module lands, which is the failure mode
    /// <c>PlatformModel</c> already records for hard-coded contributor lists.
    /// </summary>
    private static bool IsContributorSeam(INamedTypeSymbol seam) =>
        seam.Name.Length > 12 &&
        seam.Name[0] == 'I' &&
        seam.Name.EndsWith("Contributor", StringComparison.Ordinal) &&
        seam.ContainingNamespace?.ToDisplayString().StartsWith("Diaspora.", StringComparison.Ordinal) == true;

    /// <summary>
    /// <b>DSP5006.</b> Casing, separately from shape, and only as a suggestion.
    /// <para>
    /// Two rules rather than one because they carry different weight. A name that is not namespaced
    /// can collide with another module's and is a defect; a name that is <c>Recommendations.Curator</c>
    /// rather than <c>recommendations.curator</c> is inconsistent, which is worth saying and not
    /// worth failing a build over — B13 and B14 both shipped PascalCase policies, and renaming them
    /// was a change to live authorisation wiring rather than to a comment.
    /// </para>
    /// <para>
    /// <b>Those six were renamed in B1.4.1</b>, keeping their old names as registered aliases, so the
    /// rule reports nothing on the platform today. It stays at this severity for the next module: a
    /// policy name is backend-only, and the platform reserves <c>Warning</c> for a violation with no
    /// legitimate reading.
    /// </para>
    /// </summary>
    private static void ReportCasing(
        SyntaxNodeAnalysisContext context, ExpressionSyntax expression, string name)
    {
        if (!name.Equals(name.ToLowerInvariant(), StringComparison.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.CatalogueNameCasing, expression.GetLocation(), name));
        }
    }

    /// <summary>
    /// Separated, at least two segments, no empty one, nothing exotic inside one.
    /// <para>
    /// <b>Two segments, not three.</b> The <c>PermissionDefinition</c> doc comment says
    /// <c>module:resource:action</c> and the shipped catalogue disagrees with it seventy-six times:
    /// <c>bookmarks:read</c>, <c>community-life:rsvp</c> and <c>content:publish</c> are all real and
    /// all reasonable — the resource segment adds nothing when the module has one. The rule enforces
    /// what the platform does rather than what one comment says it does, and the divergence is a
    /// readiness finding rather than seventy-six build failures.
    /// </para>
    /// <para>
    /// Case is not checked here; see <see cref="ReportCasing"/>. Hyphens are allowed inside a
    /// segment because the platform already uses them — <c>identity:security-events:read</c>.
    /// </para>
    /// </summary>
    private static bool IsNamespacedName(string value, char separator)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var segments = value.Split(separator);

        if (segments.Length < 2)
        {
            return false;
        }

        foreach (var segment in segments)
        {
            if (segment.Length == 0 || !char.IsLetter(segment[0]))
            {
                return false;
            }

            foreach (var character in segment)
            {
                if (!char.IsLetterOrDigit(character) && character != '-')
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// The compile-time value behind an argument — a literal, or the constant a module declared it
    /// as. Anything computed at runtime returns null and is left alone: a rule cannot check a name
    /// it cannot see, and guessing would be worse than silence.
    /// </summary>
    private static string? Value(SyntaxNodeAnalysisContext context, ExpressionSyntax expression) =>
        context.SemanticModel.GetConstantValue(expression, context.CancellationToken).Value as string;
}
