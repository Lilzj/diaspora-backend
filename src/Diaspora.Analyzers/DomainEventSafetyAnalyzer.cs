using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Diaspora.Analyzers;

/// <summary>
/// Settlement ownership, handler boundaries, and the line between the two pipelines.
/// <para>
/// <b>Three of these four rules can only be answered by looking at the whole assembly at once.</b>
/// "Is this the only handler that settles this event?" is not a question about a file, which is
/// why they are collected during the per-class pass and reported at compilation end. The cost is
/// that an IDE shows them under full-solution analysis rather than as you type; the alternative
/// was not having them.
/// </para>
/// <para>
/// <b>The assembly is the module.</b> Every vertical is its own project, so "two handlers in this
/// module" and "two handlers in this compilation" are the same statement — and cross-module
/// fan-out, which is the entire point of a published contract, is invisible to the rule by
/// construction rather than by an exemption somebody has to remember to write.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DomainEventSafetyAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [
        Rules.HandlerDependsOnHandler,
        Rules.DuplicateDomainEventSettlement,
        Rules.DuplicateIntegrationEventSettlement,
        Rules.HandlerMixesPipelines
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var symbols = PlatformSymbols.For(context.Compilation);

        // Nothing here applies to a project that does not have the handler abstractions in scope —
        // SharedKernel, Contracts and the analyzer's own tests, among others.
        if (symbols.DomainEventHandlerBase is null && symbols.NotificationHandler is null)
        {
            return;
        }

        var settlers = new ConcurrentBag<Settler>();

        context.RegisterSyntaxNodeAction(
            node => AnalyzeType(node, symbols, settlers),
            SyntaxKind.ClassDeclaration);

        context.RegisterCompilationEndAction(end => ReportAmbiguousOwnership(end, settlers));
    }

    private static void AnalyzeType(
        SyntaxNodeAnalysisContext context, PlatformSymbols symbols, ConcurrentBag<Settler> settlers)
    {
        var declaration = (ClassDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)
            is not { IsAbstract: false, IsStatic: false } type)
        {
            return;
        }

        var domainEvents = HandledEvents(type, symbols, integration: false);
        var integrationEvents = HandledEvents(type, symbols, integration: true);
        var isHandler = domainEvents.Count > 0 || integrationEvents.Count > 0 || IsRequestHandler(type, symbols);

        if (!isHandler)
        {
            return;
        }

        ReportHandlerDependencies(context, declaration, type, symbols);

        if (domainEvents.Count > 0 && integrationEvents.Count > 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.HandlerMixesPipelines, Location(declaration), type.Name));
        }

        if (domainEvents.Count == 0 && integrationEvents.Count == 0)
        {
            return;
        }

        // Settlement is a property of the whole class, not of one method: a handler that saves in a
        // private helper is settling just as surely as one that saves in HandleAsync.
        if (!Settlement.Settles(declaration, context.SemanticModel))
        {
            return;
        }

        foreach (var handled in domainEvents)
        {
            settlers.Add(new Settler(handled, type.Name, Location(declaration), Kind.Domain));
        }

        foreach (var handled in integrationEvents)
        {
            settlers.Add(new Settler(handled, type.Name, Location(declaration), Kind.Integration));
        }
    }

    /// <summary>
    /// <b>DSP1001.</b> A constructor parameter that is itself a handler, or is the mediator that
    /// would reach one.
    /// </summary>
    private static void ReportHandlerDependencies(
        SyntaxNodeAnalysisContext context,
        ClassDeclarationSyntax declaration,
        INamedTypeSymbol type,
        PlatformSymbols symbols)
    {
        foreach (var parameter in type.InstanceConstructors.SelectMany(ctor => ctor.Parameters))
        {
            var offence = DescribeForbiddenDependency(parameter.Type, symbols);

            if (offence is null)
            {
                continue;
            }

            var location = parameter.DeclaringSyntaxReferences.Length > 0
                ? parameter.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken).GetLocation()
                : Location(declaration);

            context.ReportDiagnostic(Diagnostic.Create(
                Rules.HandlerDependsOnHandler, location, type.Name, offence));
        }
    }

    private static string? DescribeForbiddenDependency(ITypeSymbol parameter, PlatformSymbols symbols)
    {
        if (SymbolEqualityComparer.Default.Equals(parameter, symbols.Mediator) ||
            SymbolEqualityComparer.Default.Equals(parameter, symbols.Sender) ||
            SymbolEqualityComparer.Default.Equals(parameter, symbols.Publisher))
        {
            return parameter.Name + ", which reaches any handler in the process";
        }

        var isHandler =
            PlatformSymbols.DerivesFrom(parameter, symbols.DomainEventHandlerBase) ||
            PlatformSymbols.DerivesFrom(parameter, symbols.IntegrationEventHandlerBase) ||
            PlatformSymbols.Implements(parameter, symbols.NotificationHandler) ||
            PlatformSymbols.Implements(parameter, symbols.CommandHandler) ||
            PlatformSymbols.Implements(parameter, symbols.CommandHandlerWithResponse) ||
            PlatformSymbols.Implements(parameter, symbols.QueryHandler);

        return isHandler ? parameter.Name + ", which is another handler" : null;
    }

    /// <summary>
    /// <b>DSP1002 and DSP1003.</b> Reported once per event, on every settler after the first, so a
    /// module with three settling handlers gets two actionable diagnostics rather than three
    /// identical ones.
    /// </summary>
    private static void ReportAmbiguousOwnership(
        CompilationAnalysisContext context, ConcurrentBag<Settler> settlers)
    {
        foreach (var group in settlers
            .GroupBy(settler => (settler.Event, settler.EventKind))
            .Where(group => group.Count() > 1))
        {
            // Ordered so the diagnostic is the same on every build and on every machine. A rule
            // whose message depends on file enumeration order is a rule that churns diffs.
            var owners = group
                .OrderBy(settler => settler.Handler, System.StringComparer.Ordinal)
                .ToList();

            var descriptor = group.Key.EventKind == Kind.Domain
                ? Rules.DuplicateDomainEventSettlement
                : Rules.DuplicateIntegrationEventSettlement;

            foreach (var duplicate in owners.Skip(1))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    descriptor,
                    duplicate.Where,
                    group.Key.Event,
                    owners[0].Handler,
                    duplicate.Handler));
            }
        }
    }

    /// <summary>
    /// The events a type handles, from both routes: the base class every handler is supposed to use,
    /// and the raw <c>INotificationHandler&lt;DomainEventNotification&lt;T&gt;&gt;</c> that a handler
    /// serving several events has to use because the base class takes one type argument.
    /// </summary>
    private static List<string> HandledEvents(
        INamedTypeSymbol type, PlatformSymbols symbols, bool integration)
    {
        var handled = new List<string>();

        var baseDefinition = integration
            ? symbols.IntegrationEventHandlerBase
            : symbols.DomainEventHandlerBase;

        if (PlatformSymbols.BaseTypeArgument(type, baseDefinition) is { } fromBase)
        {
            handled.Add(fromBase.Name);
        }

        var wrapper = integration ? symbols.IntegrationEventNotification : symbols.DomainEventNotification;

        if (wrapper is not null)
        {
            foreach (var implemented in PlatformSymbols.ImplementedInstancesOf(type, symbols.NotificationHandler))
            {
                if (implemented.TypeArguments.FirstOrDefault() is INamedTypeSymbol notification &&
                    SymbolEqualityComparer.Default.Equals(notification.OriginalDefinition, wrapper) &&
                    notification.TypeArguments.Length == 1)
                {
                    handled.Add(notification.TypeArguments[0].Name);
                }
            }
        }

        return handled.Distinct(System.StringComparer.Ordinal).ToList();
    }

    private static bool IsRequestHandler(INamedTypeSymbol type, PlatformSymbols symbols) =>
        PlatformSymbols.Implements(type, symbols.CommandHandler) ||
        PlatformSymbols.Implements(type, symbols.CommandHandlerWithResponse) ||
        PlatformSymbols.Implements(type, symbols.QueryHandler);

    private static Location Location(ClassDeclarationSyntax declaration) =>
        declaration.Identifier.GetLocation();

    private enum Kind
    {
        Domain,
        Integration
    }

    /// <summary>
    /// One handler's claim on one event. A plain class rather than a record because netstandard2.0
    /// — which is what the compiler loads an analyzer as — has no <c>IsExternalInit</c>, and a
    /// polyfill for one four-field type is more moving parts than a constructor.
    /// </summary>
    private sealed class Settler(string @event, string handler, Location where, Kind eventKind)
    {
        public string Event { get; } = @event;

        public string Handler { get; } = handler;

        public Location Where { get; } = where;

        public Kind EventKind { get; } = eventKind;
    }
}
