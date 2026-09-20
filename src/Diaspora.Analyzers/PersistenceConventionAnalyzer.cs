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
/// The three EF Core mistakes this platform has made more than once.
/// <para>
/// <b>Each of these already has an architecture test, and each is here anyway.</b> The tests read
/// the finished model or the whole source tree and fail a build minutes after the mistake was
/// made; the analyzer fails the keystroke. B9, B10 and B11 each closed by recommending the
/// identifier rule, and B10 shipped the defect into five repositories in one phase — the gap
/// between "a test will catch this" and "the editor already did" is exactly the gap those five
/// repositories fell into.
/// </para>
/// <para>
/// The model-reading tests stay. They answer a different question: the analyzer asks whether
/// somebody wrote the configuration correctly, and the test asks what the configuration turned
/// into. A convention default that happens to be right passes one and not the other.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PersistenceConventionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The LINQ operators whose lambdas become SQL. <c>Select</c> is deliberately absent: a
    /// projection over a materialised list is ordinary mapping code, and stringifying an identifier
    /// there is how the response DTO gets built.
    /// </summary>
    private static readonly HashSet<string> QueryOperators = new(StringComparer.Ordinal)
    {
        "Where", "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending",
        "First", "FirstOrDefault", "Single", "SingleOrDefault", "Last", "LastOrDefault",
        "Any", "All", "Count", "LongCount", "Sum", "Min", "Max"
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [
        Rules.RevisionIsNotAConcurrencyToken,
        Rules.OwnedCollectionHasNoKey,
        Rules.IdentifierComparedInQuery
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

        context.RegisterSyntaxNodeAction(
            node => AnalyzeInvocation(node, symbols), SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, PlatformSymbols symbols)
    {
        // EF writes its own migrations and its own model snapshot, in this style, at this scale.
        // A rule about how a human should configure an entity has nothing to say to either.
        if (IsScaffolded(context.Node.SyntaxTree.FilePath))
        {
            return;
        }

        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax member)
        {
            return;
        }

        switch (member.Name.Identifier.ValueText)
        {
            case "Property":
                AnalyzeProperty(context, invocation);
                break;

            case "OwnsMany":
                AnalyzeOwnedCollection(context, invocation);
                break;

            default:
                if (QueryOperators.Contains(member.Name.Identifier.ValueText))
                {
                    AnalyzeQuery(context, invocation, symbols);
                }

                break;
        }
    }

    /// <summary>
    /// <b>DSP3001.</b> <c>Property(x =&gt; x.Revision)</c> and no <c>IsConcurrencyToken()</c>
    /// anywhere in the same fluent chain.
    /// <para>
    /// The whole statement is searched rather than the immediate parent, because the call can sit
    /// anywhere in the chain — <c>.HasColumnName(…).IsConcurrencyToken().HasComment(…)</c> is as
    /// correct as putting it first, and a rule that insisted on a position would be a style rule
    /// wearing a correctness rule's message.
    /// </para>
    /// </summary>
    private static void AnalyzeProperty(
        SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count != 1 ||
            invocation.ArgumentList.Arguments[0].Expression is not SimpleLambdaExpressionSyntax lambda ||
            lambda.Body is not MemberAccessExpressionSyntax accessed ||
            accessed.Name.Identifier.ValueText != "Revision")
        {
            return;
        }

        var statement = invocation.FirstAncestorOrSelf<StatementSyntax>();

        if (statement is not null && NamesMember(statement, "IsConcurrencyToken"))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rules.RevisionIsNotAConcurrencyToken,
            invocation.GetLocation(),
            Owner(context, invocation)));
    }

    /// <summary>
    /// <b>DSP3002.</b> An owned collection with no <c>HasKey</c> of its own.
    /// <para>
    /// "Of its own" is load-bearing: a nested <c>OwnsMany</c> inside the configuration block has its
    /// own <c>HasKey</c>, and without excluding those the outer mapping would borrow the inner one's
    /// declaration and pass having declared nothing.
    /// </para>
    /// </summary>
    private static void AnalyzeOwnedCollection(
        SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        var described = invocation.ArgumentList.Arguments.Count > 0
            ? invocation.ArgumentList.Arguments[0].ToString()
            : "?";

        if (invocation.ArgumentList.Arguments.LastOrDefault()?.Expression
            is not LambdaExpressionSyntax configuration)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.OwnedCollectionHasNoKey, invocation.GetLocation(), described));

            return;
        }

        var nested = configuration.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(candidate =>
                candidate.Expression is MemberAccessExpressionSyntax
                {
                    Name.Identifier.ValueText: "OwnsMany" or "OwnsOne"
                })
            .ToList();

        var declaresOwnKey = configuration.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(candidate =>
                candidate.Expression is MemberAccessExpressionSyntax
                {
                    Name.Identifier.ValueText: "HasKey"
                })
            .Any(key => !nested.Any(owned => owned.Span.Contains(key.Span)));

        if (!declaresOwnKey)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.OwnedCollectionHasNoKey, invocation.GetLocation(), described));
        }
    }

    /// <summary>
    /// <b>DSP3003.</b> The four shapes that have actually shipped, inside a lambda that becomes SQL.
    /// </summary>
    private static void AnalyzeQuery(
        SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, PlatformSymbols symbols)
    {
        if (symbols.StronglyTypedId is null)
        {
            return;
        }

        foreach (var lambda in invocation.ArgumentList.Arguments
            .Select(argument => argument.Expression)
            .OfType<LambdaExpressionSyntax>())
        {
            foreach (var inner in lambda.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (Offence(inner, context.SemanticModel, symbols) is { } offence)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Rules.IdentifierComparedInQuery, inner.GetLocation(), offence));
                }
            }
        }
    }

    private static string? Offence(
        InvocationExpressionSyntax invocation, SemanticModel model, PlatformSymbols symbols)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax member)
        {
            return null;
        }

        switch (member.Name.Identifier.ValueText)
        {
            case "ToString" when IsIdentifierExpression(member.Expression, model, symbols):
            case "CompareTo" when IsIdentifierExpression(member.Expression, model, symbols):
                return invocation.ToString();

            // string.Compare is not itself the defect — comparing a slug is the platform's
            // documented tie-break and translates everywhere. Comparing a stringified identifier
            // is, and the shape it arrives in is a ToString() on one of the operands.
            // **The fourth shape, and the one this rule missed for a year.**
            // `EF.Property<Guid>(x, nameof(T.Id)) < id` is what somebody reaches for once
            // `x.Id.Value < id` stops compiling, and it looks like it solves the problem: the
            // identifier never appears, so nothing here used to see it. It compiles, it passes
            // every test that reads a first page, and it throws
            // `InvalidCastException: Object must implement IConvertible` the first time a cursor
            // is sent — EF gives the parameter the property's own type mapping and then hands the
            // raw Guid to the identifier's value converter, which cannot take one.
            //
            // Ordering by the same expression is fine and is not reported: an ORDER BY is a
            // column reference with no parameter to type. Only the comparison needs one.
            case "Property" when IsEntityFrameworkHelper(member.Expression)
                && IsOrderingOperand(invocation):
                return invocation.ToString();

            case "Compare" when member.Expression.ToString() is "string" or "String" or "System.String":
                return invocation.ArgumentList.Arguments.Any(argument =>
                    argument.Expression.DescendantNodesAndSelf()
                        .OfType<InvocationExpressionSyntax>()
                        .Any(nested =>
                            nested.Expression is MemberAccessExpressionSyntax
                            {
                                Name.Identifier.ValueText: "ToString"
                            } stringify &&
                            IsIdentifierExpression(stringify.Expression, model, symbols)))
                    ? invocation.ToString()
                    : null;

            default:
                return null;
        }
    }

    private static bool IsEntityFrameworkHelper(ExpressionSyntax expression) =>
        expression.ToString() is "EF" or "Microsoft.EntityFrameworkCore.EF";

    /// <summary>
    /// Whether this expression is ordered against something rather than tested for equality.
    /// <para>
    /// <c>==</c> and <c>!=</c> translate: equality on a value-converted column converts the
    /// parameter through the same converter and lands on the same value. Only <c>&lt;</c>,
    /// <c>&gt;</c>, <c>&lt;=</c> and <c>&gt;=</c> are the shape that fails, and a keyset predicate
    /// needs both — so a rule that reported every <c>EF.Property</c> would also refuse the
    /// equality half of the very predicate it is asking for.
    /// </para>
    /// <para>
    /// The walk stops at the lambda: past it the expression is no longer inside the tree that
    /// becomes SQL.
    /// </para>
    /// </summary>
    private static bool IsOrderingOperand(ExpressionSyntax expression)
    {
        for (SyntaxNode? node = expression; node is not null; node = node.Parent)
        {
            if (node is LambdaExpressionSyntax)
            {
                return false;
            }

            if (node is BinaryExpressionSyntax binary
                && binary.Kind() is SyntaxKind.LessThanExpression
                    or SyntaxKind.GreaterThanExpression
                    or SyntaxKind.LessThanOrEqualExpression
                    or SyntaxKind.GreaterThanOrEqualExpression)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether an expression is a strongly typed identifier, or the <c>Value</c> unwrapped from one.
    /// Both matter: <c>x.Id.ToString()</c> and <c>x.Id.Value.ToString()</c> fail identically at
    /// translation time, and the second is the shape people reach for when the first stops
    /// compiling.
    /// </summary>
    private static bool IsIdentifierExpression(
        ExpressionSyntax expression, SemanticModel model, PlatformSymbols symbols)
    {
        if (PlatformSymbols.DerivesFrom(model.GetTypeInfo(expression).Type, symbols.StronglyTypedId))
        {
            return true;
        }

        return expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Value" } unwrapped &&
               PlatformSymbols.DerivesFrom(
                   model.GetTypeInfo(unwrapped.Expression).Type, symbols.StronglyTypedId);
    }

    private static bool NamesMember(SyntaxNode scope, string member) =>
        scope.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax access &&
                access.Name.Identifier.ValueText == member);

    /// <summary>The configuration class the offending call sits in, for a message with a subject.</summary>
    private static string Owner(SyntaxNodeAnalysisContext context, SyntaxNode node) =>
        node.FirstAncestorOrSelf<TypeDeclarationSyntax>()?.Identifier.ValueText
        ?? context.Compilation.AssemblyName
        ?? "This configuration";

    private static bool IsScaffolded(string? path) =>
        path is not null &&
        (path.Replace('\\', '/').Contains("/Migrations/") ||
         path.EndsWith("ModelSnapshot.cs", StringComparison.Ordinal));
}
