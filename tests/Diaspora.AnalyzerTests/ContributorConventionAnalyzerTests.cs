using System.Globalization;
using FluentAssertions;
using Xunit;

namespace Diaspora.AnalyzerTests;

/// <summary>
/// The contributor model's naming and shape rules.
/// </summary>
public sealed class ContributorConventionAnalyzerTests
{
    [Fact]
    public void A_permission_that_is_not_namespaced_is_reported()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class Permissions
            {
                public PermissionDefinition Bad() =>
                    new("publish", "Probe", "Publish a thing.");
            }
            """));

        ids.Should().Contain("DSP5002");
    }

    /// <summary>
    /// <b>Two segments, not three.</b> <c>PermissionDefinition</c>'s doc comment says
    /// <c>module:resource:action</c> and the shipped catalogue disagrees with it seventy-six times —
    /// <c>bookmarks:read</c> and <c>content:publish</c> are both real and both reasonable. The rule
    /// enforces what the platform does; the divergence from the comment is a readiness finding.
    /// </summary>
    [Fact]
    public void A_two_segment_permission_is_accepted()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class Permissions
            {
                public PermissionDefinition Short() =>
                    new("bookmarks:read", "Probe", "Read your bookmarks.");

                public PermissionDefinition Long() =>
                    new("probe:things:create", "Probe", "Create a thing.");

                public PermissionDefinition Hyphenated() =>
                    new("identity:security-events:read", "Probe", "Read your security events.");
            }
            """));

        ids.Should().BeEmpty();
    }

    /// <summary>
    /// Constants, not literals, because that is how every module in the platform declares a name.
    /// A rule that only matched a literal argument would apply to nothing here.
    /// </summary>
    [Fact]
    public void A_name_declared_as_a_constant_is_still_checked()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public static class ProbePermissions
            {
                public const string Bad = "publish";
            }

            public sealed class Permissions
            {
                public PermissionDefinition Declare() =>
                    new(ProbePermissions.Bad, "Probe", "Publish a thing.");
            }
            """));

        ids.Should().Contain("DSP5002");
    }

    /// <summary>
    /// <b>The drift the brief names, reported as a suggestion.</b> 'view' and 'read' both mean read
    /// and the platform has five of the first and ten of the second. A warning would demand renaming
    /// permission strings that are already minted into tokens and held by the frontend, which is a
    /// versioned authorisation change and not a guardrail's business.
    /// </summary>
    [Fact]
    public void A_synonym_for_read_is_a_suggestion_and_not_a_warning()
    {
        var diagnostics = AnalyzerHarness.Analyze(Probes.File(
            """
            public sealed class Permissions
            {
                public PermissionDefinition Analytics() =>
                    new("probe:analytics:view", "Probe", "See the numbers.");
            }
            """));

        var reported = diagnostics.Should().ContainSingle().Subject;

        reported.Id.Should().Be("DSP5004");
        reported.Severity.Should().Be(Microsoft.CodeAnalysis.DiagnosticSeverity.Info);
        reported.GetMessage(CultureInfo.InvariantCulture).Should().Contain("read");
    }

    /// <summary>
    /// A qualifier is not a synonym: <c>read-any</c> is a different authority from <c>read</c>, and
    /// flagging it would be the rule failing to read its own vocabulary.
    /// </summary>
    [Fact]
    public void A_qualified_action_is_left_alone()
    {
        AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class Permissions
            {
                public PermissionDefinition Any() =>
                    new("probe:things:read-any", "Probe", "Read anybody's thing.");
            }
            """)).Should().BeEmpty();
    }

    /// <summary>
    /// B13 and B14 both ship PascalCase policy names. Reported, at the severity that says "this is
    /// inconsistent" rather than the one that says "this is broken".
    /// </summary>
    [Fact]
    public void A_pascal_case_policy_name_is_a_suggestion()
    {
        var diagnostics = AnalyzerHarness.Analyze(Probes.File(
            """
            public sealed class Policies
            {
                public PolicyDefinition Curator() =>
                    PolicyDefinition.ForPermission("Probe.Curator", "Probe", "probe:things:create");
            }
            """));

        var reported = diagnostics.Should().ContainSingle().Subject;

        reported.Id.Should().Be("DSP5006");
        reported.Severity.Should().Be(Microsoft.CodeAnalysis.DiagnosticSeverity.Info);
    }

    [Fact]
    public void A_feature_gate_that_is_not_namespaced_is_reported()
    {
        AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class Features
            {
                public FeatureDefinition Bad() => new("publish", "Probe", "Publish a thing.");
            }
            """)).Should().Contain("DSP5001");
    }

    // ── DSP5005: the contributor's shape ─────────────────────────────────

    /// <summary>
    /// The kernel builds these with <c>Activator.CreateInstance</c> before the container exists, so
    /// a constructor dependency is not a design choice — it is a boot failure.
    /// </summary>
    [Fact]
    public void A_kernel_contributor_with_a_dependency_is_reported()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class ProbePermissionContributor : IPermissionContributor
            {
                private readonly IThingRepository _things;

                public ProbePermissionContributor(IThingRepository things) => _things = things;

                public string Module => "Probe";

                public IEnumerable<PermissionDefinition> GetPermissions() => [];
            }
            """));

        ids.Should().Contain("DSP5005");
    }

    [Fact]
    public void An_unsealed_contributor_is_reported()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public class ProbePermissionContributor : IPermissionContributor
            {
                public string Module => "Probe";

                public IEnumerable<PermissionDefinition> GetPermissions() => [];
            }
            """));

        ids.Should().Contain("DSP5005");
    }

    /// <summary>
    /// <b>The half that keeps the rule usable.</b> Only the six seams the kernel constructs itself
    /// have to be parameterless. The two dozen reference-data contributors a vertical declares are
    /// resolved from the container like anything else, and Guides ships one that legitimately takes
    /// a repository.
    /// </summary>
    [Fact]
    public void A_module_contributor_resolved_from_the_container_may_take_dependencies()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public interface IThingCategoryContributor
            {
                string Module { get; }
            }

            public sealed class ProjectedThingCategoryContributor : IThingCategoryContributor
            {
                private readonly IThingRepository _things;

                public ProjectedThingCategoryContributor(IThingRepository things) => _things = things;

                public string Module => "Probe";
            }
            """));

        ids.Should().BeEmpty();
    }

    [Fact]
    public void A_correctly_shaped_contributor_is_left_alone()
    {
        AnalyzerHarness.Ids(Probes.File(
            """
            public sealed class ProbePermissionContributor : IPermissionContributor
            {
                public string Module => "Probe";

                public IEnumerable<PermissionDefinition> GetPermissions() =>
                    [new("probe:things:create", "Probe", "Create a thing.")];
            }
            """)).Should().BeEmpty();
    }
}
