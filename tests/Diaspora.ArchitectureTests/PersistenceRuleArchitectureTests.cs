using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Diaspora.Persistence;
using Diaspora.Platform.Modules;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The persistence rules the platform knew and enforced only in prose, now enforced by the build.
/// <para>
/// <b>Three consecutive phases recommended this suite before it existed, and the cost of not having
/// it is on the record.</b> B9 asked for a rule about strongly typed identifiers in expression
/// trees; B10 did not get one and paid for it in five repositories, each of which compiled, passed
/// unit tests, and threw the first time a row existed. B11 asked again, and separately repeated a
/// request-binding defect B9 had already recorded <em>with a comment explaining it</em>. A rule a
/// human has to remember is a rule that holds until the next author.
/// </para>
/// <para>
/// <b>Why source rather than a shipped analyzer.</b> The brief allows either. A Roslyn analyzer
/// packaged into twenty projects fails the build for everybody the moment it has a bug in it, and
/// gives a worse failure message than an assertion can; these rules are narrow, and a test that
/// reads the syntax gets the same coverage with none of that blast radius. What is lost is the
/// squiggle in the editor, which is worth revisiting if the platform ever wants more than a handful
/// of these.
/// </para>
/// </summary>
public sealed class PersistenceRuleArchitectureTests
{
    /// <summary>
    /// The LINQ operators whose lambdas become SQL. Deliberately excludes <c>Select</c>: a
    /// projection over an already-materialised list is ordinary mapping code, and the three
    /// patterns below are only defects inside a predicate or an ordering.
    /// </summary>
    private static readonly HashSet<string> QueryOperators = new(StringComparer.Ordinal)
    {
        "Where", "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending",
        "First", "FirstOrDefault", "Single", "SingleOrDefault", "Last", "LastOrDefault",
        "Any", "All", "Count", "LongCount", "Sum", "Min", "Max"
    };

    /// <summary>
    /// The sanity check every source-reading rule below depends on. A glob that silently matches
    /// nothing is a test that passes for the worst possible reason.
    /// </summary>
    [Fact]
    public void The_source_tree_is_actually_being_read()
    {
        SourceTree.ProductionFiles.Should().HaveCountGreaterThan(
            1_000, "the platform is larger than this and the scan must be finding it");

        SourceTree.ProductionFiles
            .Select(file => file.Path)
            .Should().Contain(path => path.Contains("Diaspora.Persistence", StringComparison.Ordinal))
            .And.Contain(path => path.Contains("Diaspora.Modules.Housing", StringComparison.Ordinal));
    }

    // ── Rule 1: strongly typed ids never reach an expression tree ────────

    /// <summary>
    /// <b>The rule B9, B10 and B11 each closed by recommending.</b> Identifiers on this platform are
    /// value-converted <c>StronglyTypedId</c>s, and no provider translates an ordering comparison on
    /// one. The three shapes below are the three that have actually shipped:
    /// <c>string.Compare(x.Id.Value.ToString(), …)</c> in B10's five repositories,
    /// <c>x.Id.Value.CompareTo(…)</c> in B7's, and the bare <c>.Value.ToString()</c> that both grew
    /// out of. Each compiles, each passes a suite that reads before it writes, and each throws
    /// "could not be translated" the first time a row exists — which in B10 made page two of every
    /// feed a 500.
    /// <para>
    /// The fix in every case is <see cref="Persistence.Paging.CursorBoundary"/>, which excludes
    /// identifiers by set membership instead of comparing them.
    /// </para>
    /// </summary>
    [Fact]
    public void No_query_predicate_compares_a_strongly_typed_identifier()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.ProductionFiles)
        {
            foreach (var lambda in QueryLambdas(file))
            {
                foreach (var node in lambda.DescendantNodes())
                {
                    if (Describe(node) is { } offence)
                    {
                        offenders.Add($"{file.Locate(node)} — {offence}");
                    }
                }
            }
        }

        offenders.Should().BeEmpty(
            "an identifier cannot be compared in SQL; use CursorBoundary to exclude it by set " +
            "membership instead:\n" + string.Join("\n", offenders));
    }

    private static string? Describe(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
        {
            return null;
        }

        var text = invocation.ToString();

        // string.Compare is not itself the defect — comparing a slug is the platform's documented
        // tie-break and translates on every provider. What does not translate is comparing an
        // identifier, and the shape that arrives as is a ToString() on one of the operands.
        if (invocation.Expression is MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: "Compare"
            } compare
            && compare.Expression.ToString() is "string" or "String" or "System.String"
            && invocation.ArgumentList.Arguments.Any(argument =>
                argument.ToString().Contains("ToString(", StringComparison.Ordinal)))
        {
            return "string.Compare over a stringified identifier: " + text;
        }

        if (invocation.Expression is MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: "CompareTo"
            } compareTo
            && (compareTo.Expression.ToString().Contains(".Value", StringComparison.Ordinal)
                || compareTo.Expression.ToString().Contains("EF.Property", StringComparison.Ordinal)))
        {
            return "CompareTo on a strongly typed identifier: " + text;
        }

        if (invocation.Expression is MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: "ToString",
                Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Value" }
            })
        {
            return "ToString on a strongly typed identifier: " + text;
        }

        return null;
    }

    /// <summary>
    /// Every lambda handed to an operator that becomes SQL, with nested query lambdas included —
    /// a predicate inside an <c>Any</c> inside a <c>Where</c> translates too.
    /// </summary>
    private static IEnumerable<SyntaxNode> QueryLambdas(SourceFile file) =>
        file.Root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax member
                && QueryOperators.Contains(member.Name.Identifier.ValueText))
            .SelectMany(invocation => invocation.ArgumentList.Arguments)
            .Select(argument => argument.Expression)
            .Where(expression => expression is LambdaExpressionSyntax);

    // ── Rule 3: every OwnsMany declares a key ────────────────────────────

    /// <summary>
    /// <b>B11 defect A, asked of the source.</b> Its companion in
    /// <see cref="PersistenceModelArchitectureTests"/> asks the model what the key turned out to be;
    /// this asks whether anybody chose it. Both are worth having: a finalised model cannot tell a
    /// deliberate surrogate from a convention default, and source cannot tell whether a declared
    /// key is a workable one.
    /// </summary>
    [Fact]
    public void Every_owned_collection_mapping_declares_a_key()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.ProductionFiles)
        {
            foreach (var owns in file.Root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation =>
                    invocation.Expression is MemberAccessExpressionSyntax
                    {
                        Name.Identifier.ValueText: "OwnsMany"
                    }))
            {
                if (owns.ArgumentList.Arguments.LastOrDefault()?.Expression
                    is not LambdaExpressionSyntax configuration)
                {
                    // OwnsMany without a configuration lambda cannot declare anything at all.
                    offenders.Add($"{file.Locate(owns)} — no configuration block");
                    continue;
                }

                if (!DeclaresKey(configuration))
                {
                    offenders.Add($"{file.Locate(owns)} — {owns.ArgumentList.Arguments[0]}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "an owned collection left to convention gets a store-generated shadow key inside a " +
            "composite, which SQLite refuses to insert into:\n" + string.Join("\n", offenders));
    }

    /// <summary>
    /// Whether this configuration block calls <c>HasKey</c> — its own, not one belonging to an
    /// owned collection nested inside it, which would let an outer mapping borrow an inner one's
    /// declaration and pass without having made one.
    /// </summary>
    private static bool DeclaresKey(LambdaExpressionSyntax configuration)
    {
        var nested = configuration.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax
                {
                    Name.Identifier.ValueText: "OwnsMany" or "OwnsOne"
                })
            .ToList();

        return configuration.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax
                {
                    Name.Identifier.ValueText: "HasKey"
                })
            .Any(key => !nested.Any(owned => owned.Span.Contains(key.Span)));
    }

    // ── Rule: no module writes its own guarded SQL ───────────────────────

    /// <summary>
    /// <b>The rule that keeps the primitive worth having.</b> B10 and B11 both needed a guarded
    /// update and both declined to write one, which is the only reason the platform got to build it
    /// once. A module that writes its own writes it for one provider, tests it on one provider, and
    /// leaves the next module to write a second half that disagrees.
    /// <para>
    /// Raw SQL is the specific thing being kept out. <c>ExecuteUpdate</c> and <c>ExecuteDelete</c>
    /// are included because they are the ORM-flavoured route to the same place: a set-based write
    /// that bypasses the change tracker, and therefore bypasses the reconciliation that stops a
    /// stale count being written over an arbitrated one.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("ExecuteSqlRaw")]
    [InlineData("ExecuteSqlRawAsync")]
    [InlineData("ExecuteSqlInterpolated")]
    [InlineData("ExecuteSqlInterpolatedAsync")]
    [InlineData("FromSqlRaw")]
    [InlineData("FromSqlInterpolated")]
    [InlineData("ExecuteUpdate")]
    [InlineData("ExecuteUpdateAsync")]
    [InlineData("ExecuteDelete")]
    [InlineData("ExecuteDeleteAsync")]
    public void No_module_writes_its_own_guarded_update(string method)
    {
        var offenders = SourceTree.ProductionFiles
            .Where(file => file.Path.Contains("Diaspora.Modules.", StringComparison.Ordinal))
            .SelectMany(file => file.Root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation =>
                    invocation.Expression is MemberAccessExpressionSyntax member
                    && member.Name.Identifier.ValueText == method)
                .Select(file.Locate))
            .ToList();

        offenders.Should().BeEmpty(
            $"'{method}' in a vertical is a module inventing persistence the platform owns. Use " +
            "IAtomicReservationStore or IAtomicUpdateExecutor:\n" + string.Join("\n", offenders));
    }

    /// <summary>
    /// The counterpart, so the rule above cannot pass by the primitive having been deleted.
    /// </summary>
    [Fact]
    public void The_platform_still_owns_a_guarded_update_primitive()
    {
        var persistence = typeof(DiasporaDbContext).Assembly;

        persistence.GetType("Diaspora.Persistence.Atomic.IAtomicUpdateExecutor")
            .Should().NotBeNull();

        persistence.GetType("Diaspora.Persistence.Atomic.Reservations.IAtomicReservationStore")
            .Should().NotBeNull();
    }

    // ── Rule 2: [AsParameters] request types bind what they mean to ──────

    /// <summary>
    /// <b>B11 defect C, and B9's before it.</b> A non-nullable value type on an
    /// <c>[AsParameters]</c> request is a <em>required</em> query parameter, and omitting it
    /// produces a 400 with an empty body: no validation message, nothing in the logs above a debug
    /// line, and a client with no way to work out what it left out. <c>bool IncludeFacets</c> made
    /// the plainest search link on the platform a 400.
    /// <para>
    /// B9 recorded the exact shape in <c>JobBrowseParameters</c>, with a comment explaining it. B11
    /// repeated it anyway. The rule is therefore that the property is nullable — which makes it
    /// optional, with the handler choosing the default — or carries <see cref="RequiredAttribute"/>,
    /// which says somebody meant it.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_as_parameters_property_is_nullable_or_declared_required()
    {
        var offenders = new List<string>();

        foreach (var type in AsParametersTypes())
        {
            foreach (var property in type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.PropertyType.IsValueType
                    || Nullable.GetUnderlyingType(property.PropertyType) is not null
                    || property.GetCustomAttribute<RequiredAttribute>() is not null)
                {
                    continue;
                }

                offenders.Add(
                    $"{type.Name}.{property.Name} ({property.PropertyType.Name})");
            }
        }

        offenders.Should().BeEmpty(
            "a non-nullable value type on an [AsParameters] request is a required query " +
            "parameter, and leaving it out answers with an empty 400. Make it nullable, or " +
            "attribute it [Required] to say you meant it: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// The rule above is worthless if the scan finds nothing, and finding these is fiddly — the
    /// attribute sits on a parameter of a lambda, which the compiler hides in a nested display
    /// class. Seven usages exist today across four modules.
    /// </summary>
    [Fact]
    public void The_as_parameters_scan_finds_the_request_types()
    {
        AsParametersTypes().Should().HaveCountGreaterThanOrEqualTo(
            4, "several modules bind their search endpoints this way");
    }

    /// <summary>
    /// Every type any endpoint binds with <c>[AsParameters]</c>, found by walking every method in
    /// every module assembly — compiler-generated lambda classes included, which is where minimal
    /// API route handlers actually live.
    /// </summary>
    private static IReadOnlyList<Type> AsParametersTypes()
    {
        const string Attribute = "Microsoft.AspNetCore.Http.AsParametersAttribute";

        var types = new HashSet<Type>();

        foreach (var assembly in ModuleAssemblyScanner.FindPlatformAssemblies())
        {
            IEnumerable<Type> candidates;

            try
            {
                candidates = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                candidates = exception.Types.Where(type => type is not null)!;
            }

            foreach (var type in candidates)
            {
                var methods = type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.DeclaredOnly);

                foreach (var parameter in methods.SelectMany(method => method.GetParameters()))
                {
                    if (parameter.GetCustomAttributes()
                        .Any(attribute => attribute.GetType().FullName == Attribute))
                    {
                        types.Add(parameter.ParameterType);
                    }
                }
            }
        }

        return [.. types];
    }
}
