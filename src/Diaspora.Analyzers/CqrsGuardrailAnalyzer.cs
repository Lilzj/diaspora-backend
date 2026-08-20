using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Diaspora.Analyzers;

/// <summary>
/// What a command may do and what a query may not.
/// <para>
/// <b>All three rules are about a handler telling the truth about itself.</b> A query that writes,
/// a command that stages a change and never commits it, and a command that commits twice while
/// claiming to be one operation are the same failure wearing three faces: the type says one thing
/// and the body does another, and nothing in the build notices because all three compile and all
/// three pass a test that asserts the response.
/// </para>
/// <para>
/// <b>The escape hatches are deliberate and narrow.</b> Read-path counters are real —
/// a listing view is recorded on the read that caused it — so <c>[SideEffecting]</c> exists and
/// says so at the top of the file. Multi-write commands are real, so <c>[Transactional]</c> exists
/// and carries a <c>Reason</c>. Neither is a suppression; both are declarations, and the difference
/// is that a reader sees them.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CqrsGuardrailAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [
        Rules.QueryHandlerWrites,
        Rules.SequentialSavesWithoutTransaction,
        Rules.CommandHandlerNeverSaves,
        Rules.TransactionalOnHandler
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

        if (symbols.CommandHandler is null && symbols.QueryHandler is null)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            node => AnalyzeType(node, symbols), SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeType(SyntaxNodeAnalysisContext context, PlatformSymbols symbols)
    {
        var declaration = (ClassDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)
            is not { IsAbstract: false } type)
        {
            return;
        }

        if (PlatformSymbols.Implements(type, symbols.QueryHandler))
        {
            AnalyzeQueryHandler(context, declaration, type, symbols);

            return;
        }

        var isCommandHandler =
            PlatformSymbols.Implements(type, symbols.CommandHandler) ||
            PlatformSymbols.Implements(type, symbols.CommandHandlerWithResponse);

        if (isCommandHandler)
        {
            AnalyzeCommandHandler(context, declaration, type, symbols);
        }
    }

    /// <summary><b>DSP4001.</b> A query that writes, and has not said so.</summary>
    private static void AnalyzeQueryHandler(
        SyntaxNodeAnalysisContext context,
        ClassDeclarationSyntax declaration,
        INamedTypeSymbol type,
        PlatformSymbols symbols)
    {
        // Accepted on the handler or on the query. The handler is where the write is, so that is
        // the natural place; the query is where [Transactional] and [Authorize] already live, so
        // somebody will reach for it there. Neither reading is wrong and both are visible.
        var query = Argument(type, symbols.QueryHandler, position: 0);

        if (PlatformSymbols.HasAttribute(type, symbols.SideEffectingAttribute) ||
            (query is not null && PlatformSymbols.HasAttribute(query, symbols.SideEffectingAttribute)))
        {
            return;
        }

        if (Settlement.Commits(declaration, context.SemanticModel) ||
            Settlement.Stages(declaration, context.SemanticModel))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.QueryHandlerWrites, declaration.Identifier.GetLocation(), type.Name));
        }
    }

    private static void AnalyzeCommandHandler(
        SyntaxNodeAnalysisContext context,
        ClassDeclarationSyntax declaration,
        INamedTypeSymbol type,
        PlatformSymbols symbols)
    {
        // DSP4004 — the attribute in the place where it does nothing.
        if (PlatformSymbols.HasAttribute(type, symbols.TransactionalAttribute))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.TransactionalOnHandler, declaration.Identifier.GetLocation(), type.Name));
        }

        var commits = Settlement.Commits(declaration, context.SemanticModel);

        // DSP4003 — staged and never committed. Only reported when the handler stages the write
        // itself: one that hands the work to a service is that service's business to get right,
        // and second-guessing it from here would flag most of Identity.
        if (!commits && Settlement.Stages(declaration, context.SemanticModel))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.CommandHandlerNeverSaves, declaration.Identifier.GetLocation(), type.Name));
        }

        if (!commits)
        {
            return;
        }

        // The attribute is read off the command, not off the handler — TransactionBehavior closes
        // over TRequest, which is the command MediatR was given. The analyzer looked in the wrong
        // place first and reported two handlers whose commands were correctly marked, which is
        // exactly the confusion DSP4004 now names out loud.
        var command =
            Argument(type, symbols.CommandHandler, position: 0) ??
            Argument(type, symbols.CommandHandlerWithResponse, position: 0);

        if (command is null || PlatformSymbols.HasAttribute(command, symbols.TransactionalAttribute))
        {
            return;
        }

        // DSP4002 — two saves the control flow cannot avoid running one after the other.
        foreach (var block in declaration.DescendantNodes().OfType<BlockSyntax>())
        {
            if (SequentialSaves(block, context.SemanticModel) < 2)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                Rules.SequentialSavesWithoutTransaction,
                declaration.Identifier.GetLocation(),
                type.Name,
                command.Name));

            return;
        }
    }

    /// <summary>
    /// The request type a handler is closed over — <c>TCommand</c> from
    /// <c>ICommandHandler&lt;TCommand, TResponse&gt;</c>, or <c>TQuery</c> from its query twin.
    /// </summary>
    private static INamedTypeSymbol? Argument(
        INamedTypeSymbol handler, INamedTypeSymbol? definition, int position)
    {
        if (definition is null)
        {
            return null;
        }

        return handler.AllInterfaces
            .Where(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, definition) &&
                candidate.TypeArguments.Length > position)
            .Select(candidate => candidate.TypeArguments[position] as INamedTypeSymbol)
            .FirstOrDefault(argument => argument is not null);
    }

    /// <summary>
    /// Saves that are unconditionally sequential within one block.
    /// <para>
    /// Only plain expression and declaration statements are counted, and only as direct children.
    /// A save inside an <c>if</c>, a <c>try</c>, a loop or an early-returning branch is a save on
    /// <em>a</em> path, not on <em>this</em> path, and counting those is how this rule would flag
    /// twenty handlers that commit exactly once per execution — which is what a textual count of
    /// <c>SaveChangesAsync</c> per file does, and why it is not what this does.
    /// </para>
    /// </summary>
    private static int SequentialSaves(BlockSyntax block, SemanticModel model) =>
        block.Statements
            .Where(statement => statement is ExpressionStatementSyntax or LocalDeclarationStatementSyntax)
            .Count(statement => statement.DescendantNodesAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .Any(invocation => Settlement.IsSave(invocation, model)));
}
