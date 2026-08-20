using Diaspora.Analyzers;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Diaspora.AnalyzerTests;

/// <summary>
/// The seam a future AI phase will land on, guarded before it exists.
/// </summary>
public sealed class AiIsolationAnalyzerTests
{
    /// <summary>
    /// The <c>using</c> is where this arrives, and it is the last moment at which removing it is
    /// free. By the time an API key is in configuration and a client is in the container, the
    /// vertical is undeployable without a network and the change is somebody's afternoon.
    /// </summary>
    [Theory]
    [InlineData("using OpenAI.Chat;")]
    [InlineData("using Azure.AI.OpenAI;")]
    [InlineData("using Microsoft.SemanticKernel;")]
    [InlineData("using Pinecone;")]
    public void A_business_module_importing_a_vendor_is_reported(string import)
    {
        // The namespaces do not resolve — no such package is referenced anywhere in this platform,
        // which is the point — so the probe is compiled on its own rather than through Probes.File,
        // and the rule is a syntax rule for exactly that reason.
        var source =
            $$"""
            {{import}}

            namespace Diaspora.Modules.Probe;

            public sealed class Ranker;
            """;

        var ids = AnalyzeIgnoringUnresolvedImport(source, AnalyzerHarness.BusinessModule);

        ids.Should().Contain("DSP6001");
    }

    /// <summary>
    /// A module whose name merely starts the same way is not a vendor. The rule matches a namespace
    /// root followed by a dot or nothing, so <c>Diaspora.Modules.Openings</c> is safe and
    /// <c>OpenAI.Chat</c> is not.
    /// </summary>
    [Fact]
    public void A_module_whose_name_only_looks_like_a_vendor_is_left_alone()
    {
        var ids = AnalyzeIgnoringUnresolvedImport(
            """
            using System;

            namespace Diaspora.Modules.Probe;

            public sealed class Openings;
            """,
            AnalyzerHarness.BusinessModule);

        ids.Should().BeEmpty();
    }

    /// <summary>
    /// <b>The adapter half.</b> The rule keeps vendors out of verticals, not out of the platform:
    /// an adapter assembly is exactly where a model client is supposed to live, behind
    /// <c>IRankingStrategy</c> or <c>ISimilarityProvider</c>.
    /// </summary>
    [Fact]
    public void An_infrastructure_adapter_may_name_a_vendor()
    {
        var ids = AnalyzeIgnoringUnresolvedImport(
            """
            using OpenAI.Chat;

            namespace Diaspora.Infrastructure.Ai;

            public sealed class Ranker;
            """,
            "Diaspora.Infrastructure");

        ids.Should().BeEmpty();
    }

    /// <summary>
    /// Compiles the probe without the harness's insistence that it build cleanly. A
    /// <c>using</c> of a package nobody references cannot resolve, and that unresolved import is
    /// precisely the thing under test.
    /// </summary>
    private static IReadOnlyList<string> AnalyzeIgnoringUnresolvedImport(
        string source, string assemblyName)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var reported = compilation
            .WithAnalyzers([new AiIsolationAnalyzer()], new AnalyzerOptions([]))
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();

        reported.Should().NotContain(
            diagnostic => diagnostic.Id == "AD0001", "the analyzer must not throw");

        return [.. reported.Select(diagnostic => diagnostic.Id)];
    }
}
