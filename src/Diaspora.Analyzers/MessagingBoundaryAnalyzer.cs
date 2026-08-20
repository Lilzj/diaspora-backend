using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Diaspora.Analyzers;

/// <summary>
/// The B1.1 and B1.2 boundaries, kept where the compiler cannot reach.
/// <para>
/// <b>Most of the messaging boundary is already a compile error, and that is the point of what is
/// left here.</b> B1.2 separated the two event hierarchies so the type system refuses the four
/// routes that mattered; these three rules cover the ways the separation can be undone rather
/// than crossed — a type that joins both hierarchies back together, a module that goes around the
/// publisher to the transport underneath it, and a vertical that decides to notify somebody
/// itself.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MessagingBoundaryAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The plumbing under <c>IIntegrationEventPublisher</c>. Naming any of these from a module is
    /// the bypass: each one writes or ships a message outside the caller's transaction.
    /// </summary>
    private static readonly string[] TransportSurfaces =
    [
        "IOutboxWriter", "IOutboxStore", "IOutboxTransport", "IOutboxDispatcher", "OutboxMessageFactory"
    ];

    /// <summary>
    /// The Communication Platform's write path, named from outside it. <c>INotificationPublisher</c>
    /// is the platform's port and <c>NotificationRequest</c> is its payload; a vertical touching
    /// either has decided to notify somebody without B13's say.
    /// </summary>
    private static readonly string[] NotificationSurfaces =
    [
        "INotificationPublisher", "NotificationRequest", "INotificationDispatcher"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [
        Rules.EventIsBothKinds,
        Rules.PublicationBypassesOutbox,
        Rules.NotificationCreatedOutsideCommunication
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
        var assembly = context.Compilation.AssemblyName;

        if (symbols.DomainEvent is not null && symbols.IntegrationEvent is not null)
        {
            context.RegisterSymbolAction(
                symbol => AnalyzeEventType(symbol, symbols), SymbolKind.NamedType);
        }

        // The kernel and its adapters are the transport, and the Communication Platform is the
        // notification. Neither rule has anything to say to the code that implements it.
        if (!ModuleScope.IsBusinessModule(assembly))
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            node => AnalyzeIdentifier(node, ModuleScope.IsCommunication(assembly)),
            SyntaxKind.IdentifierName);
    }

    /// <summary><b>DSP2001.</b> One type, both hierarchies.</summary>
    private static void AnalyzeEventType(SymbolAnalysisContext context, PlatformSymbols symbols)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind == TypeKind.Interface)
        {
            return;
        }

        if (PlatformSymbols.Implements(type, symbols.DomainEvent) &&
            PlatformSymbols.Implements(type, symbols.IntegrationEvent))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.EventIsBothKinds, type.Locations.FirstOrDefault(), type.Name));
        }
    }

    /// <summary>
    /// <b>DSP2002 and DSP2003.</b> Both are "a module named a type it has no business naming", so
    /// both are answered the same way: by the identifier, wherever it appears — a field, a
    /// constructor parameter, a local, a <c>new</c>, a generic argument.
    /// <para>
    /// Matched on the simple name rather than the full symbol on purpose. A module cannot reference
    /// the outbox at all today (the architecture suite enforces it), so the symbol would not resolve
    /// and a semantic rule would see nothing. The name is what a developer would write first, and
    /// catching it at that moment is the whole value.
    /// </para>
    /// </summary>
    private static void AnalyzeIdentifier(SyntaxNodeAnalysisContext context, bool isCommunication)
    {
        var identifier = (IdentifierNameSyntax)context.Node;
        var name = identifier.Identifier.ValueText;

        // A declaration of the type itself, or a member access on something else, would double-count.
        // Only the use of a name as a type reference is interesting.
        if (identifier.Parent is MemberAccessExpressionSyntax member && member.Name == identifier)
        {
            return;
        }

        if (Array.IndexOf(TransportSurfaces, name) >= 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.PublicationBypassesOutbox, identifier.GetLocation(), name));

            return;
        }

        if (!isCommunication && Array.IndexOf(NotificationSurfaces, name) >= 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.NotificationCreatedOutsideCommunication, identifier.GetLocation(), name));
        }
    }
}
