using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Diaspora.Analyzers;

/// <summary>
/// The seam a future AI phase will land on, protected before it exists.
/// <para>
/// <b>This rule guards a decision B14 already made and nothing yet enforces.</b> The
/// Recommendation Platform ships deterministic ranking behind <c>IRankingStrategy</c>,
/// deterministic similarity behind <c>ISimilarityProvider</c> and a projection behind
/// <c>IRecommendationCandidateSource</c> — each shaped so that an embedding model, a vector index
/// or an LLM ranker can replace the implementation without a line of business code changing. The
/// value of that shape survives exactly as long as nobody takes the shortcut.
/// </para>
/// <para>
/// <b>What the shortcut costs, concretely.</b> A vendor client named inside a vertical makes that
/// vertical undeployable without an API key, unrunnable in the test suite without a network, and
/// non-deterministic in a codebase whose recommendation module is explicitly built to be
/// explainable. None of those show up in a code review of the pull request that adds the
/// <c>using</c>.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AiIsolationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Namespace roots that belong to a model provider, a vector store or an orchestration
    /// framework. Matched on the first segment of a <c>using</c>, so <c>OpenAI.Chat</c> and
    /// <c>OpenAI</c> are both caught and a module called <c>Diaspora.Modules.Openings</c> is not.
    /// </summary>
    private static readonly (string Root, string Vendor)[] Vendors =
    [
        ("OpenAI", "the OpenAI SDK"),
        ("Anthropic", "the Anthropic SDK"),
        ("Azure.AI", "Azure OpenAI"),
        ("Microsoft.SemanticKernel", "Semantic Kernel"),
        ("Microsoft.ML", "ML.NET"),
        ("Google.Cloud.AIPlatform", "Vertex AI"),
        ("GenerativeAI", "the Gemini SDK"),
        ("Mscc.GenerativeAI", "the Gemini SDK"),
        ("Mistral", "the Mistral SDK"),
        ("Cohere", "the Cohere SDK"),
        ("Ollama", "a local LLM runtime"),
        ("LLama", "a local LLM runtime"),
        ("LangChain", "an LLM orchestration framework"),
        ("Pinecone", "a vector database"),
        ("Qdrant", "a vector database"),
        ("Weaviate", "a vector database"),
        ("Milvus", "a vector database"),
        ("HuggingFace", "a model hub client")
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [Rules.AiVendorInBusinessModule];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (!ModuleScope.IsBusinessModule(start.Compilation.AssemblyName))
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeUsing, SyntaxKind.UsingDirective);
        });
    }

    private static void AnalyzeUsing(SyntaxNodeAnalysisContext context)
    {
        var directive = (UsingDirectiveSyntax)context.Node;
        var imported = directive.Name?.ToString();

        if (imported is null)
        {
            return;
        }

        foreach (var (root, vendor) in Vendors)
        {
            var matches = imported.Equals(root, StringComparison.Ordinal) ||
                          imported.StartsWith(root + ".", StringComparison.Ordinal);

            if (!matches)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                Rules.AiVendorInBusinessModule,
                directive.GetLocation(),
                context.Compilation.AssemblyName,
                vendor));

            return;
        }
    }
}
