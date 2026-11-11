using System.Collections.Immutable;
using System.Reflection;
using Diaspora.Application.Abstractions;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// BSOLID-001's second verification criterion: <b><c>[RequirePermission()]</c> with no argument
/// does not compile.</b>
/// <para>
/// <b>This can only be proved by code that fails to build, which cannot be checked into a test
/// project.</b> So the snippet is compiled in-process against the real platform assemblies and the
/// errors are the assertion — the same technique <see cref="MessagingCompilerEnforcementTests"/>
/// established, pointed at a different guarantee.
/// </para>
/// <para>
/// <b>Why it is worth a test at all.</b> A required constructor argument is the entire difference
/// between the new vocabulary and the old one. <c>[Authorize]</c> permitted every field to be
/// null, so an author who meant to name a permission and was interrupted left behind an attribute
/// that read as a decision and admitted every caller. The compiler is what makes that state
/// unreachable now, and a test that says so survives somebody "simplifying" the constructor to
/// take an optional argument.
/// </para>
/// </summary>
public class AuthorizationCompilerEnforcementTests
{
    [Fact]
    public void A_permission_requirement_with_no_argument_does_not_compile()
    {
        var errors = Compile("""
            [RequirePermission]
            public sealed record ReadThingQuery : IQuery<string>;
            """);

        errors.Should().NotBeEmpty(
            "a permission requirement that names no permission is the empty [Authorize] this "
            + "vocabulary replaced, and it must not be expressible");
    }

    [Fact]
    public void A_policy_requirement_with_no_argument_does_not_compile()
    {
        var errors = Compile("""
            [RequirePolicy]
            public sealed record ReadThingQuery : IQuery<string>;
            """);

        errors.Should().NotBeEmpty();
    }

    /// <summary>
    /// The anonymous marker too. Its reason is the only thing distinguishing a deliberate public
    /// request from one nobody classified, so an argumentless form would give back exactly what
    /// BSEC-003 took away.
    /// </summary>
    [Fact]
    public void An_anonymous_declaration_with_no_reason_does_not_compile()
    {
        var errors = Compile("""
            [AllowAnonymous]
            public sealed record ReadThingQuery : IQuery<string>;
            """);

        errors.Should().NotBeEmpty();
    }

    [Fact]
    public void An_authentication_requirement_with_no_reason_does_not_compile()
    {
        var errors = Compile("""
            [RequireAuthentication]
            public sealed record ReadThingQuery : IQuery<string>;
            """);

        errors.Should().NotBeEmpty();
    }

    /// <summary>
    /// <b>The control.</b> Without this the four tests above would pass on a snippet that failed to
    /// compile for some unrelated reason — a missing using, a renamed interface — and would keep
    /// passing after the attributes were deleted entirely.
    /// </summary>
    [Fact]
    public void Each_declaration_compiles_when_it_says_what_it_requires()
    {
        var errors = Compile("""
            [RequirePermission("probe:things:read")]
            public sealed record ReadOneQuery : IQuery<string>;

            [RequirePolicy("probe.things.read")]
            public sealed record ReadTwoQuery : IQuery<string>;

            [RequireAuthentication("The caller's own things.")]
            public sealed record ReadThreeQuery : IQuery<string>;

            [AllowAnonymous("Published reference content.")]
            public sealed record ReadFourQuery : IQuery<string>;
            """);

        errors.Should().BeEmpty(
            "the legal forms must compile, or the four refusals above prove nothing");
    }

    /// <summary>
    /// The old attribute is gone, and the compiler is what proved the 863-attribute migration was
    /// complete. Asserted so that reintroducing it as a convenience is a deliberate act.
    /// </summary>
    [Fact]
    public void The_optional_everything_attribute_no_longer_exists()
    {
        var errors = Compile("""
            [Authorize(Permission = "probe:things:read")]
            public sealed record ReadThingQuery : IQuery<string>;
            """);

        errors.Should().NotBeEmpty(
            "[Authorize] carried three nullable fields and permitted a state meaning 'no check'. "
            + "Deleting it is what let the compiler prove every one of its 863 uses had been "
            + "migrated — including one on a SignalR hub, where the CQRS attribute would have "
            + "silently left the socket unauthenticated");
    }

    private static ImmutableArray<Diagnostic> Compile(string declarations)
    {
        var source =
            $$"""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Diaspora.Application.Abstractions;
            using Diaspora.SharedKernel.Results;

            namespace AuthorizationProbe;

            {{declarations}}
            """;

        var compilation = CSharpCompilation.Create(
            assemblyName: "AuthorizationProbe",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: ReferenceSet(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
    }

    private static List<MetadataReference> ReferenceSet()
    {
        var roots = new[]
        {
            typeof(object).Assembly,
            typeof(Console).Assembly,
            typeof(Task).Assembly,
            typeof(SharedKernel.Results.Result).Assembly,
            typeof(RequirePermissionAttribute).Assembly,
            typeof(MediatR.INotification).Assembly
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var references = new List<MetadataReference>();

        void Add(Assembly assembly)
        {
            if (string.IsNullOrEmpty(assembly.Location) || !seen.Add(assembly.Location))
            {
                return;
            }

            references.Add(MetadataReference.CreateFromFile(assembly.Location));

            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                try
                {
                    Add(Assembly.Load(reference));
                }
                catch (Exception exception) when (
                    exception is FileNotFoundException or FileLoadException or BadImageFormatException)
                {
                    // A reference this test project did not resolve is one the snippet cannot
                    // name either, so leaving it out keeps the reference set honest.
                }
            }
        }

        foreach (var root in roots)
        {
            Add(root);
        }

        return references;
    }
}
