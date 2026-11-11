using System.Reflection;
using Diaspora.Application.Abstractions;
using Diaspora.Modules.Creator;
using Diaspora.Modules.Creator.Application.Activity;
using Diaspora.Platform.Modules;
using FluentAssertions;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// Every CQRS request says whether it needs an authenticated caller.
/// <para>
/// <b>BSEC-003 was not a missing annotation; it was the absence of any way to notice one.</b>
/// <c>AuthorizationBehavior</c> returns <c>next()</c> when <see cref="AuthorizeAttribute"/> is
/// absent, <c>FallbackPolicy</c> is deliberately <c>null</c>, and the architecture test that a
/// comment in <c>IdentityAuthenticationRegistration</c> said would compensate for both had never
/// been written. The one test occupying its name asserted that <i>more than a hundred</i> endpoints
/// were authorised — true, unfalsifiable by any individual endpoint, and green throughout.
/// </para>
/// <para>
/// <b>Reflection, not source text.</b> This asks the same question
/// <c>AuthorizationBehavior</c> asks at runtime —
/// <c>typeof(TRequest).GetCustomAttribute&lt;AuthorizeAttribute&gt;()</c> — of the same compiled
/// types, so the rule and the behaviour cannot disagree about what counts as annotated. A regex
/// over the source was tried first and was wrong in both directions: it reported 286 unannotated
/// types, 38 of which carry the attribute on a line a multi-line attribute argument hid from it,
/// and it missed <c>DeleteMyAccountCommand</c> entirely. Acting on that number would have marked
/// correctly protected commands as public.
/// </para>
/// <para>
/// <b>Why there is an exemption list rather than a flipped default.</b> 247 request types carry
/// neither attribute. Most are correctly public — published reference content, sign-in,
/// registration, signature-verified webhooks — some are authorised by their HTTP route, and some
/// are reached only from background services where there is no caller at all. Refusing all of them
/// today would refuse live traffic, so they are enumerated in
/// <c>RequestsAwaitingAuthorizationReview.txt</c> and the list is held at its current size or
/// smaller. The effect is fail-closed for everything written from now on, and a visible burn-down
/// for what already exists. The technique is the platform's own: a dated exemption list, as used
/// for the 25 deprecated permission keys and ADR-020's 16 retirements.
/// </para>
/// <para>
/// <b>Plain <c>[Fact]</c>s, and the list lives under <c>tests/</c>.</b> Both deliberate: a rule
/// whose input sits under the gitignored <c>docs/</c> tree skips on every clean checkout, which is
/// how nineteen rules once reported coverage they were not providing. See
/// <see cref="LocalDocumentation"/>.
/// </para>
/// </summary>
public sealed class RequestAuthorizationArchitectureTests
{
    private const string ReviewListPath =
        "tests/Diaspora.ArchitectureTests/RequestsAwaitingAuthorizationReview.txt";

    /// <summary>
    /// The size of the review list on 9 November 2026, when BSEC-003's detection half landed.
    /// <para>
    /// A ceiling, not an expectation. Annotating a type and deleting its line needs no change here;
    /// adding an unannotated request type does, which is the point.
    /// </para>
    /// </summary>
    private const int ReviewListSizeAtBaseline = 247;

    private static readonly Lazy<List<Type>> Requests = new(FindRequests);

    private static readonly Lazy<List<string>> ReviewList = new(LoadReviewList);

    /// <summary>
    /// The sanity check the rest rest on: a population this rule cannot discover is a rule that
    /// passes by finding nothing.
    /// </summary>
    [Fact]
    public void The_request_population_is_discovered_rather_than_listed()
    {
        Requests.Value.Should().HaveCountGreaterThan(
            900, "the platform declares around 970 queries and commands across 21 modules");
    }

    /// <summary>
    /// A request type carries <see cref="AuthorizeAttribute"/>, carries
    /// <see cref="AllowAnonymousAttribute"/>, or is on the dated review list. Nothing else.
    /// </summary>
    [Fact]
    public void Every_request_type_declares_whether_it_needs_authorization()
    {
        Undeclared()
            .Except(ReviewList.Value, StringComparer.Ordinal)
            .Should().BeEmpty(
                "a request type with neither attribute is served to whoever reaches it, and "
                + "nothing else in this repository would say so. Add "
                + "[Authorize(Permission = …)] if a caller is required, or "
                + "[AllowAnonymous(\"why\")] if the public reach is intended — the reason is "
                + "required because the value of the marker over an absent attribute is that "
                + "somebody wrote down what they meant");
    }

    /// <summary>
    /// Every name on the review list is still an unannotated request type.
    /// <para>
    /// This is what stops the list going stale, and it is the half that does the real work. Without
    /// it, annotating a type would leave its name behind as a standing exemption, so removing the
    /// annotation later would be silently permitted — the list would grant back exactly what it was
    /// written to take away.
    /// </para>
    /// </summary>
    [Fact]
    public void The_review_list_names_only_request_types_that_are_still_unannotated()
    {
        var undeclared = Undeclared().ToHashSet(StringComparer.Ordinal);
        var known = Requests.Value
            .Select(Describe)
            .ToHashSet(StringComparer.Ordinal);

        ReviewList.Value
            .Where(entry => !known.Contains(entry))
            .Should().BeEmpty(
                "{0} names something that is not a request type — a rename, or a type that no "
                + "longer exists. Delete the line", ReviewListPath);

        ReviewList.Value
            .Where(known.Contains)
            .Where(entry => !undeclared.Contains(entry))
            .Should().BeEmpty(
                "this request type is annotated now, so its line in {0} is a standing exemption "
                + "nobody needs. Delete it — that is how the burn-down is recorded",
                ReviewListPath);
    }

    /// <summary>
    /// The list is empty, and stays empty.
    /// <para>
    /// <b>Replaced "the list only shrinks" on 10 Nov 2026, because it finished shrinking.</b> All
    /// 247 entries are classified and <c>AuthorizationBehavior</c> now refuses a request carrying
    /// neither attribute. A ceiling of 247 was the right rule while the sweep was in progress; now
    /// it would be a door left open, because a new unannotated request type could be waved through
    /// by adding a line here.
    /// </para>
    /// <para>
    /// So the rule is <c>BeEmpty</c>, and the escape hatch is gone on purpose: the only way to add
    /// a request type to this platform is to say what it requires.
    /// </para>
    /// </summary>
    [Fact]
    public void The_review_list_is_empty_and_stays_empty()
    {
        ReviewList.Value.Should().BeEmpty(
            "{0} held {1} entries when BSEC-003's detection landed and holds none now. A line "
            + "here would be a request type nobody classified, admitted by editing a text file "
            + "instead of by deciding what it requires",
            ReviewListPath,
            ReviewListSizeAtBaseline);
    }

    /// <summary>
    /// The one instance the audit confirmed reachable without a token stays authorised.
    /// <para>
    /// Pinned by name because it is the finding's live half. The module declares no route-level
    /// authorisation anywhere in <c>Diaspora.Modules.Creator/Api</c>, so this attribute is the only
    /// thing standing between the route and an anonymous caller — the 401 itself is asserted over
    /// HTTP in the integration suite, which is the only place that can.
    /// </para>
    /// </summary>
    [Fact]
    public void The_creator_activity_query_requires_the_same_permission_as_its_siblings()
    {
        var requirement = typeof(GetMyCreatorActivityQuery)
            .GetCustomAttribute<RequirePermissionAttribute>();

        requirement.Should().NotBeNull(
            "GET /api/v1/creators/me/activity was reachable unauthenticated (BSEC-003)");

        requirement!.Permission.Should().Be(
            CreatorPermissions.ProfileRead,
            "every sibling caller-scoped creator read takes ProfileRead — earnings, revenue, "
            + "reputation, milestones, referral programme, referral history — and Participate is "
            + "what the module requires of an act");
    }

    // ─── Discovery ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A request declares its requirement with exactly one of four attributes, or it is undeclared.
    /// </summary>
    /// <remarks>
    /// Four rather than two since BSOLID-001 split <c>[Authorize]</c> by question. Listing them
    /// here rather than probing a shared base type is deliberate: there is no base type, because
    /// an abstract one would reintroduce the thing the split removed — somewhere for a request to
    /// be "authorized" without saying how.
    /// </remarks>
    private static IEnumerable<string> Undeclared() =>
        Requests.Value
            .Where(type => type.GetCustomAttribute<RequirePermissionAttribute>() is null)
            .Where(type => type.GetCustomAttribute<RequirePolicyAttribute>() is null)
            .Where(type => type.GetCustomAttribute<RequireAuthenticationAttribute>() is null)
            .Where(type => type.GetCustomAttribute<AllowAnonymousAttribute>() is null)
            .Select(Describe)
            .OrderBy(entry => entry, StringComparer.Ordinal);

    private static string Describe(Type type) =>
        $"{type.Assembly.GetName().Name}|{type.Name}";

    private static List<Type> FindRequests()
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies()
            .Append(typeof(IQuery<>).Assembly)
            .Distinct()
            .ToList();

        return [.. assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(IsRequest)
            .Distinct()];
    }

    private static bool IsRequest(Type type) =>
        Array.Exists(
            type.GetInterfaces(),
            contract => contract == typeof(ICommand)
                || (contract.IsGenericType
                    && (contract.GetGenericTypeDefinition() == typeof(IQuery<>)
                        || contract.GetGenericTypeDefinition() == typeof(ICommand<>))));

    private static List<string> LoadReviewList()
    {
        var path = Path.Combine(SourceTree.Root.FullName, ReviewListPath);

        File.Exists(path).Should().BeTrue(
            "{0} is tracked precisely so that it is here on every checkout", ReviewListPath);

        return [.. File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))];
    }
}
