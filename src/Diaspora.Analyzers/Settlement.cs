using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Diaspora.Analyzers;

/// <summary>
/// What it means for a handler to <em>settle</em> something.
/// <para>
/// <b>This is the distinction the whole domain-event family rests on.</b> The brief that produced
/// these rules is precise about it: multiple handlers for one event is not the problem, multiple
/// handlers settling one consequence is. So the analyzer needs a definition of settlement it can
/// apply mechanically, and it is this — a handler settles when it commits, when it mutates the
/// change tracker through a repository, or when it announces the outcome to other modules.
/// </para>
/// <para>
/// A handler that reads, projects, logs, emits telemetry or updates a cache does none of those and
/// is free to coexist with as many siblings as it likes. That is the low-noise half of the rule,
/// and it is the reason this is a scan for three specific shapes rather than "does this handler do
/// anything".
/// </para>
/// </summary>
internal static class Settlement
{
    /// <summary>
    /// Repository members that stage a write. Deliberately not <c>Save</c>: staging and committing
    /// are counted separately so the CQRS rules can tell one from the other.
    /// </summary>
    private static readonly string[] StagingMembers = ["Add", "Update", "Remove", "Delete", "Restore"];

    public static bool Settles(TypeDeclarationSyntax declaration, SemanticModel model) =>
        Commits(declaration, model) ||
        Stages(declaration, model) ||
        Announces(declaration, model);

    /// <summary>A call to <c>SaveChangesAsync</c>, on the unit of work or on a context.</summary>
    public static bool Commits(SyntaxNode scope, SemanticModel model) =>
        Invocations(scope).Any(invocation => IsSave(invocation, model));

    public static bool IsSave(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax member ||
            member.Name.Identifier.ValueText is not ("SaveChangesAsync" or "SaveChanges"))
        {
            return false;
        }

        // Name alone would be enough in this codebase, but resolving keeps the rule honest against
        // a helper that happens to be called SaveChanges and saves nothing.
        var symbol = model.GetSymbolInfo(invocation).Symbol;

        return symbol is null || symbol.ContainingType is null || IsPersistenceSurface(symbol.ContainingType);
    }

    /// <summary>A call to <c>Add</c>, <c>Update</c>, <c>Remove</c> and friends on a repository.</summary>
    public static bool Stages(SyntaxNode scope, SemanticModel model) =>
        Invocations(scope).Any(invocation => IsStagingCall(invocation, model));

    public static bool IsStagingCall(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax member ||
            !StagingMembers.Contains(member.Name.Identifier.ValueText))
        {
            return false;
        }

        // The receiver has to be a repository. Without this the rule would count every
        // List<T>.Add and Dictionary.Remove in the handler, which is most of them.
        var receiver = model.GetTypeInfo(member.Expression).Type;

        return receiver is not null && IsRepository(receiver);
    }

    /// <summary>A call to the integration-event publisher: the module telling the world.</summary>
    public static bool Announces(SyntaxNode scope, SemanticModel model) =>
        Invocations(scope).Any(invocation =>
            invocation.Expression is MemberAccessExpressionSyntax member &&
            member.Name.Identifier.ValueText is "PublishAsync" or "Publish" &&
            model.GetTypeInfo(member.Expression).Type is { } receiver &&
            receiver.Name is "IIntegrationEventPublisher" or "IntegrationEventPublisher");

    public static bool IsRepository(ITypeSymbol type) =>
        type.Name.EndsWith("Repository", System.StringComparison.Ordinal) ||
        type.Name.EndsWith("Store", System.StringComparison.Ordinal) ||
        type.Name.EndsWith("Writer", System.StringComparison.Ordinal);

    private static bool IsPersistenceSurface(ITypeSymbol type) =>
        type.Name is "IUnitOfWork" or "DbContext" ||
        type.Name.EndsWith("DbContext", System.StringComparison.Ordinal) ||
        type.Name.EndsWith("UnitOfWork", System.StringComparison.Ordinal) ||
        IsRepository(type);

    private static System.Collections.Generic.IEnumerable<InvocationExpressionSyntax> Invocations(
        SyntaxNode scope) =>
        scope.DescendantNodes().OfType<InvocationExpressionSyntax>();
}
