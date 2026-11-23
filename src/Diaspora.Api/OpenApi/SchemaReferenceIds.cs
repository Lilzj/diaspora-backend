using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization.Metadata;
using Diaspora.Platform.Modules;
using Microsoft.AspNetCore.OpenApi;

namespace Diaspora.Api.OpenApi;

/// <summary>
/// What a schema in the published contract is called, and why the framework's answer was not
/// enough.
/// <para>
/// <b>A component name in OpenAPI is global; a type name in C# is per-assembly.</b> The default
/// reference id is the CLR type's simple name, so two modules that each declare a
/// <c>BookmarkResponse</c> produce one component — and whichever type the document generator
/// reaches first decides what every route of <em>both</em> modules is documented as sending. The
/// other type is never emitted at all.
/// </para>
/// <para>
/// <b>This was not hypothetical and it was not small.</b> On 23 November 2026 sixteen component
/// names each stood for two to four different contract types, and <b>158 of the 1,092
/// operations</b> were documented with a shape that belonged to another module. The document said
/// <c>POST /api/v1/trust/verifications</c> took <c>{ notes }</c>; the route binds nine properties.
/// It said <c>POST /api/v1/community/bookmarks</c> took <c>{ targetType, targetId }</c>; that route
/// binds <c>{ contentType, contentId, folderId }</c>. The client had found the second by hand and
/// written "the published contract is wrong about this route" in a comment, which was the only
/// place it was recorded.
/// </para>
/// <para>
/// <b>And the winner was discovery order.</b> Nothing chose CommunityProgramme's
/// <c>MoneyResponse</c> over Financial's, Creator's and Analytics' — the generator met it first. A
/// reordering nobody would review could have changed what dozens of routes claim to return, with
/// only the <c>contracts/</c> diff to say so.
/// </para>
/// <para>
/// <b>Three rules, each paid for.</b> Only an ambiguous name is qualified, because qualifying
/// everything would rename about 1,800 components to fix sixteen, and a diff that size in the one
/// file clients read is a diff nobody checks. The qualifier is joined with a dot, because the
/// first version of this joined it with nothing and turned Community's <c>PollResponse</c> into
/// <c>CommunityPollResponse</c> — a name CommunityProgramme already declares — and silently dropped
/// CommunityProgramme's type from the document: the very defect, reintroduced by its fix. A dot
/// cannot appear in a C# identifier, so a qualified name can never equal an unqualified one. And
/// <see cref="Claim"/> refuses to give one id to two types, which fails the build, because the
/// build regenerates this document.
/// </para>
/// </summary>
internal static class SchemaReferenceIds
{
    private static readonly Lazy<HashSet<string>> AmbiguousNames = new(FindAmbiguousNames);

    private static readonly ConcurrentDictionary<string, Type> Assigned =
        new(StringComparer.Ordinal);

    /// <summary>
    /// The reference id for a type: the framework's own, unless a name inside it is ambiguous.
    /// </summary>
    /// <remarks>
    /// Built by rewriting the default rather than by composing a new one. The default already
    /// handles generic nesting and the <c>Of</c>/<c>And</c> spelling that
    /// <c>ApiResponseOfIReadOnlyListOfBookmarkResponse</c> depends on, and reproducing that here to
    /// change one token in it would put a second implementation of the framework's naming in this
    /// repository, to be kept in step with a runtime nobody here controls.
    /// </remarks>
    public static string? For(JsonTypeInfo type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var id = OpenApiOptions.CreateDefaultSchemaReferenceId(type);

        if (id is null)
        {
            // Null means "inline this schema rather than reference it". Preserved exactly: an
            // inlined schema has no name to be ambiguous about.
            return null;
        }

        return Claim(
            Rewrite(id, [.. Closure(type.Type)], AmbiguousNames.Value),
            type.Type,
            Assigned);
    }

    /// <summary>
    /// Records that <paramref name="id"/> names <paramref name="type"/>, and refuses if it already
    /// names something else.
    /// <para>
    /// <b>This is the guarantee; the rest is policy.</b> Qualifying the names known to be ambiguous
    /// prevents the collisions anyone has thought of. This catches the ones nobody has: two types
    /// in one assembly under different namespaces, a qualified name that happens to spell another
    /// type's name, a generic whose arguments compose into somebody else's component. Each of those
    /// would otherwise produce a document in which every <c>$ref</c> still resolves — so no
    /// structural check would object — and one type has quietly disappeared.
    /// </para>
    /// <para>
    /// <c>T</c> and <c>T?</c> are one component and are allowed to share an id; any other pair is
    /// refused with both names, which turns into a failed build here because
    /// <c>OpenApiGenerateDocumentsOnBuild</c> runs this on every compile of <c>Diaspora.Api</c>.
    /// </para>
    /// </summary>
    internal static string Claim(string id, Type type, ConcurrentDictionary<string, Type> assigned)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(assigned);

        var claimant = Nullable.GetUnderlyingType(type) ?? type;
        var holder = assigned.GetOrAdd(id, claimant);

        if (holder != claimant)
        {
            throw new InvalidOperationException(
                $"The OpenAPI component '{id}' would name two different types: "
                + $"'{holder.FullName}' and '{claimant.FullName}'. One of them would be dropped "
                + "from the published contract and every route that returns it documented with "
                + "the other's properties. Rename one of them, or extend "
                + nameof(SchemaReferenceIds) + " so the two compose to different names.");
        }

        return id;
    }

    /// <summary>
    /// Rewrites the ambiguous names inside a composed id, matching the longest name first.
    /// <para>
    /// <b>A plain string replace is wrong here and the document contains the counterexample.</b>
    /// Trust declares <c>ModerationCaseReportResponse</c> while four modules declare
    /// <c>ReportResponse</c>; replacing the shorter name wherever it appears would rewrite the tail
    /// of the longer one and invent a component nothing declares. Matching the longest candidate at
    /// each position is what makes the rewrite a rename rather than a substitution.
    /// </para>
    /// <para>
    /// Occurrences of one name are consumed in the order <see cref="Closure"/> yields them, which
    /// is the order the framework composes them in, so a closure holding two different types of
    /// the same simple name qualifies each from its own assembly rather than twice from the first.
    /// </para>
    /// </summary>
    internal static string Rewrite(
        string id,
        IReadOnlyList<Type> closure,
        IReadOnlySet<string> ambiguous)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(closure);
        ArgumentNullException.ThrowIfNull(ambiguous);

        var pending = closure
            .Where(part => ambiguous.Contains(part.Name))
            .ToList();

        if (pending.Count == 0)
        {
            return id;
        }

        // Longest first, so a name that contains another is recognised as itself. Spelled the way
        // the framework spells them in an id: without the arity a generic's CLR name carries.
        var candidates = closure
            .Select(SimpleName)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(name => name.Length)
            .ToList();

        var rewritten = new StringBuilder(id.Length);

        for (var index = 0; index < id.Length;)
        {
            var match = candidates.Find(name =>
                string.CompareOrdinal(id, index, name, 0, name.Length) == 0);

            if (match is null)
            {
                rewritten.Append(id[index]);
                index++;
                continue;
            }

            var owner = pending.Find(part => SimpleName(part) == match);

            if (owner is null)
            {
                rewritten.Append(match);
            }
            else
            {
                pending.Remove(owner);
                rewritten.Append(Qualify(owner));
            }

            index += match.Length;
        }

        return rewritten.ToString();
    }

    /// <summary>
    /// A type and every type inside it, generic arguments and element types included.
    /// <para>
    /// The ambiguity that matters is rarely the outermost type. Every response on this platform is
    /// wrapped — <c>ApiResponse&lt;IReadOnlyList&lt;T&gt;&gt;</c> — so the name that decides what a
    /// route documents is two levels in, and a check that read only <c>type.Name</c> would have seen
    /// <c>ApiResponse</c> and found nothing wrong with any of the 158.
    /// </para>
    /// </summary>
    internal static IEnumerable<Type> Closure(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        yield return type;

        if (type.HasElementType && type.GetElementType() is { } element)
        {
            foreach (var inner in Closure(element))
            {
                yield return inner;
            }
        }

        foreach (var argument in type.IsGenericType ? type.GetGenericArguments() : [])
        {
            foreach (var inner in Closure(argument))
            {
                yield return inner;
            }
        }
    }

    /// <summary>
    /// The name a type gets when its simple name is not its own: the declaring assembly's last
    /// segment, a dot, and the name — so <c>Diaspora.Modules.Community</c>'s
    /// <c>BookmarkResponse</c> becomes <c>Community.BookmarkResponse</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The dot is the point.</b> OpenAPI allows it in a component name and C# forbids it in an
    /// identifier, so a qualified name cannot coincide with any type's unqualified one. Joining with
    /// nothing looked tidier and was wrong on the first build: Community's <c>PollResponse</c>
    /// became <c>CommunityPollResponse</c>, which CommunityProgramme declares, and that module's
    /// type vanished from the document with every reference to it still resolving.
    /// </para>
    /// <para>
    /// Read off the assembly rather than the namespace. A contract type's namespace carries a
    /// <c>Contracts.V1.Responses</c> tail that says nothing about which module owns it, and two
    /// modules spell that tail identically — which is how the collision arises in the first place.
    /// </para>
    /// </remarks>
    internal static string Qualify(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var assembly = type.Assembly.GetName().Name ?? string.Empty;
        var owner = assembly[(assembly.LastIndexOf('.') + 1)..];

        return owner + "." + SimpleName(type);
    }

    /// <summary>
    /// A type's name as an id spells it: without the <c>`1</c> arity a generic's CLR name carries.
    /// </summary>
    internal static string SimpleName(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var tick = type.Name.IndexOf('`', StringComparison.Ordinal);
        return tick < 0 ? type.Name : type.Name[..tick];
    }

    /// <summary>
    /// Simple names that more than one platform assembly declares publicly.
    /// <para>
    /// Derived, never listed. A list would be right on the day it was written and would then say
    /// nothing about the collision somebody introduces next month — which is the failure this type
    /// exists to end, not to reproduce one layer up.
    /// </para>
    /// <para>
    /// Keyed by the CLR name, arity included, because that is the distinction the runtime makes:
    /// <c>SourcedValueResponse</c> and <c>SourcedValueResponse&lt;T&gt;</c> compose to different
    /// ids and never collided, though a source scan reports them as one name.
    /// </para>
    /// <para>
    /// Over-inclusive on purpose: a name shared by a contract type and an unrelated public type
    /// elsewhere — CommunityProgramme's <c>PollResponse</c> aggregate, say — is qualified too. That
    /// costs one component name and keeps the question answerable without deciding here what counts
    /// as a contract type, a judgement that would itself drift.
    /// </para>
    /// </summary>
    private static HashSet<string> FindAmbiguousNames()
    {
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        var ambiguous = new HashSet<string>(StringComparer.Ordinal);

        foreach (var assembly in ModuleAssemblyScanner.FindPlatformAssemblies())
        {
            var name = assembly.GetName().Name ?? string.Empty;

            foreach (var type in ExportedTypes(assembly))
            {
                if (!owners.TryAdd(type.Name, name) && owners[type.Name] != name)
                {
                    ambiguous.Add(type.Name);
                }
            }
        }

        return ambiguous;
    }

    /// <summary>
    /// An assembly whose public surface cannot be read is skipped, as it is in the scanner this
    /// borrows its assembly list from. A missing reference is a packaging problem; taking the
    /// document generator down over one would turn it into a failure to build at all.
    /// </summary>
    private static Type[] ExportedTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or FileLoadException or TypeLoadException
                or ReflectionTypeLoadException)
        {
            return [];
        }
    }
}
