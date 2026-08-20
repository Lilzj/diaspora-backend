using System.Collections.Immutable;
using System.Reflection;
using Diaspora.Analyzers;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Events;
using Diaspora.Platform.Messaging;
using Diaspora.SharedKernel.Primitives;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Diaspora.AnalyzerTests;

/// <summary>
/// Compiles a snippet against the real platform and runs the real analyzers over it.
/// <para>
/// <b>Nothing here is a fake.</b> The reference set is taken from the loaded assemblies of the
/// platform types themselves, exactly as <c>MessagingCompilerEnforcementTests</c> does, so a rule
/// that resolves <c>DomainEventHandler&lt;T&gt;</c> is resolving the one the platform ships. A
/// harness that stubbed the base classes would pass on the day somebody renamed one.
/// </para>
/// <para>
/// <b>The assembly name is a parameter because half the rules depend on it.</b> "Only the
/// Communication Platform creates notifications" and "a business module must not name an AI
/// vendor" are both statements about which project the code is in, and the analyzer reads that
/// from <c>Compilation.AssemblyName</c>. A probe therefore says which module it is pretending to
/// be, and the tests exercise both sides of every boundary.
/// </para>
/// </summary>
internal static class AnalyzerHarness
{
    /// <summary>A stand-in vertical. Matches the kernel's rule for what a business module is.</summary>
    public const string BusinessModule = "Diaspora.Modules.Probe";

    /// <summary>The Communication Platform, by its real assembly name.</summary>
    public const string Communication = "Diaspora.Modules.Notifications";

    private static readonly Lazy<ImmutableArray<MetadataReference>> References = new(ResolveReferences);

    /// <summary>
    /// Every diagnostic the pack reports for <paramref name="source"/>, ordered so an assertion can
    /// name them in a stable order.
    /// </summary>
    public static ImmutableArray<Diagnostic> Analyze(
        string source, string assemblyName = BusinessModule)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, path: assemblyName + "/Probe.cs")],
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // A snippet that does not compile would make every assertion below meaningless: an
        // analyzer given a broken semantic model reports whatever it can resolve, which is usually
        // nothing, and the test would pass by finding no violation in code that has no meaning.
        var compileErrors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (compileErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "The probe does not compile, so its analyzer results would be meaningless:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, compileErrors.Select(error => error.ToString())));
        }

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new DomainEventSafetyAnalyzer(),
            new MessagingBoundaryAnalyzer(),
            new PersistenceConventionAnalyzer(),
            new CqrsGuardrailAnalyzer(),
            new ContributorConventionAnalyzer(),
            new AiIsolationAnalyzer());

        var reported = compilation
            .WithAnalyzers(analyzers, new AnalyzerOptions([]))
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();

        // AD0001 is the compiler reporting that an analyzer threw. Left unchecked it reads as a
        // clean run: the rule reported nothing because it crashed before it could.
        var crashed = reported.Where(diagnostic => diagnostic.Id == "AD0001").ToList();

        if (crashed.Count > 0)
        {
            throw new InvalidOperationException(
                "An analyzer threw while running:" + Environment.NewLine +
                string.Join(Environment.NewLine, crashed.Select(error => error.ToString())));
        }

        return [.. reported.OrderBy(diagnostic => diagnostic.Id, StringComparer.Ordinal)];
    }

    /// <summary>The ids reported, for the common assertion.</summary>
    public static IReadOnlyList<string> Ids(string source, string assemblyName = BusinessModule) =>
        [.. Analyze(source, assemblyName).Select(diagnostic => diagnostic.Id)];

    private static ImmutableArray<MetadataReference> ResolveReferences()
    {
        var roots = new[]
        {
            typeof(object).Assembly,
            typeof(Console).Assembly,
            typeof(Task).Assembly,

            // System.Linq.Queryable, named explicitly. Without it the probe's LINQ binds to the
            // Enumerable overloads instead — which compiles differently, fails differently, and
            // would quietly mean the identifier rule was being tested against in-memory LINQ that
            // never becomes SQL.
            typeof(Queryable).Assembly,
            typeof(System.Linq.Expressions.Expression).Assembly,
            typeof(IMessage).Assembly,                        // Diaspora.SharedKernel
            typeof(IIntegrationEvent).Assembly,               // Diaspora.Contracts
            typeof(IIntegrationEventPublisher).Assembly,      // Diaspora.Platform
            typeof(DomainEventHandler<>).Assembly,            // Diaspora.Application
            typeof(Domain.Abstractions.IUnitOfWork).Assembly, // Diaspora.Domain
            typeof(MediatR.INotification).Assembly,
            typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly,
            typeof(Microsoft.EntityFrameworkCore.RelationalPropertyBuilderExtensions).Assembly
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var references = ImmutableArray.CreateBuilder<MetadataReference>();

        void Add(Assembly assembly)
        {
            if (!assembly.IsDynamic &&
                !string.IsNullOrEmpty(assembly.Location) &&
                seen.Add(assembly.Location))
            {
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
        }

        foreach (var assembly in roots)
        {
            Add(assembly);
        }

        // System.Runtime and the rest of the facades are never named directly and are required for
        // anything to bind. Everything already loaded into this test host covers them.
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Add(assembly);
        }

        return references.ToImmutable();
    }
}
