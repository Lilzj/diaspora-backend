using Diaspora.Application.Identity.Authorization;
using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Contributors;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// What a service caller may hold, and what may mint one.
/// <para>
/// <b>A service token is the one principal on this platform that skips every live-state check.</b>
/// The branch in <c>AccessTokenValidationHandler</c> returns before the session lookup, the account
/// lookup and the security stamp, which is correct — a service has no session to check — and it
/// means the permission claims the signature covers are the <em>only</em> gate. Everything here is
/// about keeping that gate narrow.
/// </para>
/// <para>
/// BSEC-013 and BSOLID-007 of the November 2026 pre-launch audit. The issuer they are about is
/// retired under <c>DSP7001</c>; these are the two properties that outlive it, because the platform
/// still accepts a service token minted elsewhere.
/// </para>
/// </summary>
public sealed class ServiceAuthorityArchitectureTests
{
    private static readonly IReadOnlyList<PermissionDefinition> AllPermissions =
        ModuleAssemblyScanner
            .CreateAll<IPermissionContributor>(ModuleAssemblyScanner.FindPlatformAssemblies())
            .SelectMany(contributor => contributor.GetPermissions())
            .ToList();

    /// <summary>
    /// <b>The rule the audit asked for, and the inverse of one that already existed.</b>
    /// <c>AccessTierSemanticsArchitectureTests.Every_administrative_capability_is_reached_by_role_or_by_service_token</c>
    /// asserts the forward direction — an administrative permission is reached by a role, by a
    /// service token, or by nothing. It is satisfied by a service permission that <em>also</em>
    /// names a role, which is the one combination that matters here: it would put authority
    /// intended for a machine into a human's token, where none of the service-token branch's
    /// assumptions hold.
    /// </summary>
    [Fact]
    public void No_service_permission_is_reachable_by_a_role_or_a_tier()
    {
        var service = AllPermissions
            .Where(permission => permission.Audience == PermissionAudience.Service)
            .ToList();

        service.Should().NotBeEmpty(
            "the platform declares at least one machine-to-machine permission, and a rule with " +
            "nothing on its left-hand side proves nothing");

        var reachable = service
            .Where(permission => permission.Roles.Count > 0 || permission.MinimumTier is not null)
            .Select(permission =>
                $"{permission.Key}: audience=Service, roles=[{string.Join(", ", permission.Roles)}], " +
                $"minimumTier={permission.MinimumTier?.ToString() ?? "none"}")
            .ToList();

        reachable.Should().BeEmpty(
            "a service permission must be unreachable by progression and by role, which is " +
            "ADR-002's Identity/Trust separation expressed as data. A member holding one would " +
            "carry authority whose only intended holder presents a token with no session, no " +
            "account and no security stamp:\n" + string.Join("\n", reachable));
    }

    /// <summary>
    /// <b>The classifier agrees, which is what makes the data rule and the code rule one rule.</b>
    /// <c>CapabilityClassifier</c> short-circuits on the service audience before it looks at tiers
    /// or roles, so a service permission is administrative whatever else it says. If the two ever
    /// disagreed, the capabilities endpoint and the authorisation check would answer differently
    /// about the same permission.
    /// </summary>
    [Fact]
    public void Every_service_permission_classifies_as_administrative()
    {
        var misclassified = AllPermissions
            .Where(permission => permission.Audience == PermissionAudience.Service)
            .Where(permission =>
                CapabilityClassifier.Classify(permission) != CapabilityCategory.Administrative)
            .Select(permission => permission.Key)
            .ToList();

        misclassified.Should().BeEmpty(
            "a service permission is authority no member can hold: " +
            string.Join(", ", misclassified));
    }

    /// <summary>
    /// <b>The registration stays gone.</b> Retiring the issuer is a compile error anywhere it is
    /// named, so this would be caught by the build — but it is asserted here anyway because the
    /// failure it guards against is somebody deleting the <c>[Obsolete]</c> attribute, and that is
    /// a one-line edit that makes every other signal disappear at once.
    /// <para>
    /// Read off the source rather than off a built container: the question is whether anybody
    /// wrote the line, and a reflective check would need a host and would not say where.
    /// </para>
    /// </summary>
    [Fact]
    public void Nothing_registers_the_retired_service_token_issuer()
    {
        var offenders = SourceTree.ProductionFiles
            .SelectMany(file => file.Root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation => invocation.Expression.ToString()
                    .Contains("IServiceTokenIssuer", StringComparison.Ordinal))
                .Select(file.Locate))
            .Order(StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "IServiceTokenIssuer is retired under DSP7001 and has no production caller: the " +
            "Trust Engine runs outside this process and signs its own token. Reviving it means " +
            "a kid-scoped signing key as well, so the member key cannot mint one:\n" +
            string.Join("\n", offenders));
    }

    /// <summary>
    /// <b>And nothing constructs the implementation either.</b> The registration rule above reads
    /// the generic argument; this reads the <c>new</c>. They are different edits and a reader
    /// looking for one would not find the other.
    /// </summary>
    [Fact]
    public void Nothing_in_production_constructs_the_retired_issuer()
    {
        var offenders = SourceTree.ProductionFiles
            .SelectMany(file => file.Root.DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .Where(creation => creation.Type.ToString()
                    .EndsWith("ServiceTokenIssuer", StringComparison.Ordinal))
                .Select(file.Locate))
            .Order(StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "nothing in this platform mints a service token:\n" + string.Join("\n", offenders));
    }
}
