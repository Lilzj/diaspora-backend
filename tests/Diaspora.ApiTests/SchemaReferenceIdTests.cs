using System.Collections.Concurrent;
using Diaspora.Api.OpenApi;
using FluentAssertions;
using Xunit;

namespace Diaspora.ApiTests;

/// <summary>
/// What a component in the published contract is called when two modules want the same word.
/// <para>
/// <b>The document is the one artefact a second repository reads</b>, and until 23 November 2026
/// a component name in it could stand for two or three different C# types. The generator emitted
/// whichever it reached first, left the rest out, and documented their routes with that one's
/// properties — seventeen names, and 147 of the 1,092 operations described with a shape that
/// belonged to another module. RMD-006.
/// </para>
/// <para>
/// <c>PublishedContractArchitectureTests</c> in the architecture suite asserts the outcome against
/// the emitted document, which is the guarantee that matters. These assert the rules that produce
/// it, because the document can only show the cases the platform happens to contain today, and two
/// of the hazards below are not among them yet.
/// </para>
/// </summary>
public sealed class SchemaReferenceIdTests
{
    private const string Owner = "ApiTests";

    /// <summary>
    /// A name no second module claims is left exactly as the framework composed it.
    /// <para>
    /// This is the whole reason the rewrite is selective. Qualifying every component would rename
    /// about 1,800 of them to disambiguate seventeen, and a diff that size in the file clients read
    /// is a diff nobody checks — which would hide the next real change rather than reveal it.
    /// </para>
    /// </summary>
    [Fact]
    public void A_name_only_one_module_declares_is_left_alone()
    {
        var id = SchemaReferenceIds.Rewrite(
            "ApiResponseOfReportResponse",
            [typeof(ReportResponse)],
            new HashSet<string>(StringComparer.Ordinal));

        id.Should().Be("ApiResponseOfReportResponse");
    }

    /// <summary>
    /// An ambiguous name is qualified with the module that declares it, joined with a dot.
    /// </summary>
    [Fact]
    public void An_ambiguous_name_is_qualified_with_its_module()
    {
        var id = SchemaReferenceIds.Rewrite(
            "ApiResponseOfReportResponse",
            [typeof(ReportResponse)],
            new HashSet<string>(StringComparer.Ordinal) { nameof(ReportResponse) });

        id.Should().Be("ApiResponseOf" + Owner + ".ReportResponse");
    }

    /// <summary>
    /// <b>The fix reintroduced the defect, and this is the line that would have said so.</b>
    /// <para>
    /// The first version joined module and name with nothing, so Community's <c>PollResponse</c>
    /// became <c>CommunityPollResponse</c> — a type CommunityProgramme declares under exactly that
    /// name. One component, two types again, and CommunityProgramme's poll vanished from the
    /// document together with the two schemas only it referenced, with every <c>$ref</c> still
    /// resolving. A dot is legal in an OpenAPI component name and illegal in a C# identifier, so a
    /// qualified name can never spell another type's own.
    /// </para>
    /// </summary>
    [Fact]
    public void A_qualified_name_cannot_spell_any_type_s_own_name()
    {
        var qualified = SchemaReferenceIds.Qualify(typeof(ReportResponse));

        qualified.Should().Be(Owner + ".ReportResponse");
        qualified.Should().Contain(".", "no C# simple name contains one, which is the guarantee");
    }

    /// <summary>
    /// <b>The hazard a plain string replace walks straight into, and the document contains the
    /// counterexample.</b> Trust declares <c>ModerationCaseReportResponse</c> while other modules
    /// declare <c>ReportResponse</c>. Replacing the shorter name wherever it occurs rewrites the tail
    /// of the longer one and invents a component nothing declares — and because every <c>$ref</c>
    /// would still resolve to it, no structural check on the document would notice.
    /// </summary>
    [Fact]
    public void A_longer_name_that_ends_with_an_ambiguous_one_is_not_rewritten_inside()
    {
        var id = SchemaReferenceIds.Rewrite(
            "ModerationCaseReportResponseOfReportResponse",
            [typeof(ModerationCaseReportResponse), typeof(ReportResponse)],
            new HashSet<string>(StringComparer.Ordinal) { nameof(ReportResponse) });

        id.Should().Be("ModerationCaseReportResponseOf" + Owner + ".ReportResponse");
    }

    /// <summary>
    /// A generic is matched the way the framework spells it in an id, without the arity its CLR
    /// name carries — otherwise an ambiguous generic would be looked for as <c>Page`1</c> in a
    /// string that only ever says <c>Page</c>, and left bare.
    /// </summary>
    [Fact]
    public void An_ambiguous_generic_is_found_without_its_arity()
    {
        var id = SchemaReferenceIds.Rewrite(
            "PageOfReportResponse",
            [typeof(Page<ReportResponse>), typeof(ReportResponse)],
            new HashSet<string>(StringComparer.Ordinal) { typeof(Page<>).Name });

        id.Should().Be(Owner + ".PageOfReportResponse");
    }

    /// <summary>
    /// The name that decides what a route documents is usually two levels in, so the closure has
    /// to go there.
    /// <para>
    /// Every response on this platform is wrapped — <c>ApiResponse&lt;IReadOnlyList&lt;T&gt;&gt;</c>
    /// — so a check that read only the outermost type would have seen <c>ApiResponse</c> and found
    /// nothing wrong with any of the 147 operations.
    /// </para>
    /// </summary>
    [Fact]
    public void The_closure_reaches_a_type_two_levels_of_generics_down()
    {
        var names = SchemaReferenceIds
            .Closure(typeof(IReadOnlyList<IReadOnlyList<ReportResponse>>))
            .Select(type => type.Name)
            .ToList();

        names.Should().Contain(nameof(ReportResponse));
    }

    /// <summary>
    /// Two types of the same name in one id each consume their own closure entry.
    /// <para>
    /// A rewrite that qualified the first occurrence and left the second would produce an id that
    /// is half true, and the <c>$ref</c> would still resolve, so nothing structural would object.
    /// Both owners read <c>ApiTests</c> here because both types are declared in this assembly; what
    /// is under test is that the second occurrence is rewritten at all.
    /// </para>
    /// </summary>
    [Fact]
    public void Each_occurrence_of_a_repeated_name_is_rewritten()
    {
        var id = SchemaReferenceIds.Rewrite(
            "PairOfReportResponseAndReportResponse",
            [typeof(ReportResponse), typeof(Elsewhere.ReportResponse)],
            new HashSet<string>(StringComparer.Ordinal) { nameof(ReportResponse) });

        id.Should().Be(
            "PairOf" + Owner + ".ReportResponseAnd" + Owner + ".ReportResponse");
    }

    // ── The guarantee: one id, one type ──────────────────────────────────────────────────────

    /// <summary>
    /// The same type may claim its id as often as the generator asks.
    /// </summary>
    [Fact]
    public void A_type_may_claim_its_own_id_again()
    {
        var assigned = new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);

        SchemaReferenceIds.Claim("ReportResponse", typeof(ReportResponse), assigned);
        var again = () => SchemaReferenceIds.Claim(
            "ReportResponse", typeof(ReportResponse), assigned);

        again.Should().NotThrow();
    }

    /// <summary>
    /// <c>T</c> and <c>T?</c> are one component, and refusing them would fail the build over a
    /// difference the document does not have.
    /// </summary>
    [Fact]
    public void A_value_type_and_its_nullable_form_share_an_id()
    {
        var assigned = new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);

        SchemaReferenceIds.Claim("Moment", typeof(Moment), assigned);
        var nullable = () => SchemaReferenceIds.Claim("Moment", typeof(Moment?), assigned);

        nullable.Should().NotThrow();
    }

    /// <summary>
    /// <b>Two different types are refused one id, with both named.</b>
    /// <para>
    /// This is the property every rule above serves, checked at the moment it would be broken.
    /// Qualifying known-ambiguous names prevents the collisions anyone has thought of; this catches
    /// the rest — two same-named types under different namespaces of one assembly, a qualified
    /// name that spells another type's, a generic whose arguments compose into somebody else's
    /// component. The platform has two latent same-assembly pairs today, <c>CheckInRequest</c> in
    /// CommunityLife and <c>FeatureFlag</c> in Platform, where only one of each reaches the
    /// document. The day the second does, the build stops here instead of a type disappearing.
    /// </para>
    /// </summary>
    [Fact]
    public void Two_different_types_are_refused_one_id()
    {
        var assigned = new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);

        SchemaReferenceIds.Claim("ReportResponse", typeof(ReportResponse), assigned);
        var collision = () => SchemaReferenceIds.Claim(
            "ReportResponse", typeof(Elsewhere.ReportResponse), assigned);

        collision.Should().Throw<InvalidOperationException>()
            .WithMessage("*'ReportResponse'*two different types*")
            .Which.Message.Should().Contain(typeof(Elsewhere.ReportResponse).FullName!);
    }

    private sealed record ReportResponse;

    private sealed record ModerationCaseReportResponse;

    private sealed record Page<T>(T Item);

    private readonly record struct Moment(long Ticks);

    /// <summary>
    /// Somewhere else to declare the same simple name, which is the whole point of it. A nested
    /// type rather than a second namespace because a file-scoped namespace is the only one a file
    /// may have, and <c>Type.Name</c> — the name the document uses — is the same either way.
    /// </summary>
    private static class Elsewhere
    {
        internal sealed record ReportResponse;
    }
}
