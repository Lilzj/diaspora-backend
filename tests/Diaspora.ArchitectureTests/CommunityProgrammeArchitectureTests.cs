using System.Reflection;
using Diaspora.Modules.CommunityProgramme;
using Diaspora.Modules.CommunityProgramme.Domain.Polls;
using Diaspora.Platform.Modules;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// B18's boundaries, asserted rather than described.
/// <para>
/// The module's whole claim is that it is an extension of Community Life rather than a second one:
/// it owns what a body offers its members, and owns nothing about who belongs, what money moves, how
/// anything is delivered or what is ranked. Every one of those is a sentence in the module's
/// description, and each of the tests below is what stops it becoming a sentence that used to be
/// true.
/// </para>
/// </summary>
public sealed class CommunityProgrammeArchitectureTests
{
    private static readonly Assembly Module = typeof(CommunityProgrammeModule).Assembly;

    private static readonly IReadOnlyList<string> References =
        [.. Module.GetReferencedAssemblies().Select(name => name.Name!)];

    // ── The module's place in the graph ──────────────────────────────────────────────────────

    /// <summary>
    /// <b>B18 composes last, and the number is load-bearing.</b> It reads B12's role edge and B15's
    /// currency catalogue, and the seam registries are built in module order.
    /// </summary>
    [Fact]
    public void The_module_composes_after_community_life_and_financial()
    {
        var modules = new ModuleRegistry().DiscoverModules().Modules;

        var programme = modules.Single(
            registration => registration.Module.Name == CommunityProgrammeModuleMetadata.Name);

        var communityLife = modules.Single(
            registration => registration.Module.Name == "CommunityLife");

        var financial = modules.Single(registration => registration.Module.Name == "Financial");

        programme.Module.Order.Should().BeGreaterThan(communityLife.Module.Order);
        programme.Module.Order.Should().BeGreaterThan(financial.Module.Order);
    }

    /// <summary>
    /// <b>Three module references, and no more.</b> Community Life for the organization and its role
    /// edge; Financial for the currency catalogue; Community for one content source. A fourth would
    /// be a boundary decision nobody made.
    /// </summary>
    /// <remarks>
    /// <b>This was two, and Community is the third.</b> It is here for exactly one class —
    /// <c>Infrastructure/Community/PollContentSource.cs</c> — because
    /// <c>CommunityContentType.Poll</c> was declared in B6's enum and claimed by nothing, so a poll
    /// could not be reported. This module owns the poll that is independently addressable
    /// (<c>/communities/polls/{pollId}</c>); Community's own <c>Poll</c> hangs off a post and is
    /// reported by reporting that post.
    /// <para>
    /// The direction is the one every feature module takes — Jobs, Marketplace, Housing, Guides,
    /// Providers, CommunityLife and Conversations all reference Community to claim a content type,
    /// and Community references none of them back. The assertion stays exhaustive rather than
    /// becoming a "contains" check, so a fourth reference still has to be argued for in a diff.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_module_references_only_community_life_financial_and_community()
    {
        var moduleReferences = References
            .Where(name => name.StartsWith("Diaspora.Modules.", StringComparison.Ordinal))
            .ToList();

        moduleReferences.Should().BeEquivalentTo(
            [
                "Diaspora.Modules.CommunityLife",
                "Diaspora.Modules.Financial",
                "Diaspora.Modules.Community"
            ]);
    }

    /// <summary>
    /// <b>The module does not reference the Communication Platform.</b> It publishes integration
    /// events and B13 consumes them; a reference in the other direction would be the first cycle in
    /// the graph B2.6 exists to keep acyclic.
    /// </summary>
    [Fact]
    public void The_module_does_not_reference_notifications_or_recommendations()
    {
        References.Should().NotContain("Diaspora.Modules.Notifications");
        References.Should().NotContain("Diaspora.Modules.Recommendations");
        References.Should().NotContain("Diaspora.Modules.Conversations");
    }

    /// <summary>A module never names the composition root.</summary>
    [Fact]
    public void The_module_does_not_reference_the_host()
    {
        References.Should().NotContain("Diaspora.Api");
        References.Should().NotContain("Diaspora.Operations");
    }

    // ── What it must not do itself ───────────────────────────────────────────────────────────

    /// <summary>
    /// <b>No realtime transport, no push vendor, no email vendor.</b> Delivery is B13's, and this
    /// module publishes a fact rather than composing a notification.
    /// </summary>
    [Fact]
    public void The_module_names_no_delivery_technology()
    {
        var forbidden = new[]
        {
            "Microsoft.AspNetCore.SignalR",
            "FirebaseAdmin",
            "SendGrid",
            "Twilio",
            "MailKit"
        };

        foreach (var name in forbidden)
        {
            References.Should().NotContain(
                reference => reference.Contains(name, StringComparison.OrdinalIgnoreCase),
                $"{name} is somebody else's job");
        }
    }

    /// <summary>
    /// <b>No type in the module names SignalR or a hub.</b> Checked over the type graph rather than
    /// the reference list, because a transitive reference would not appear above.
    /// </summary>
    [Fact]
    public void No_type_in_the_module_touches_a_realtime_hub()
    {
        var offenders = Module
            .GetTypes()
            .Where(type =>
                type.FullName?.Contains("Hub", StringComparison.Ordinal) == true
                || type.FullName?.Contains("SignalR", StringComparison.Ordinal) == true)
            .ToList();

        offenders.Should().BeEmpty(
            "realtime is B13.2's transport, and B18 does not need one");
    }

    /// <summary>
    /// <b>The module creates no financial state.</b> It expresses an intent to charge — a price on a
    /// tier — and B15 owns everything that follows. No type in this module's <em>domain</em> is a
    /// payment, a donation, a wallet or a transaction.
    /// <para>
    /// <b>Scoped to the domain, and B15.1 is why.</b> The rule as first written matched every class
    /// in the assembly by name, which caught
    /// <c>Contracts.V1.Requests.BindMembershipPaymentRequest</c> — a request body carrying one
    /// identifier, forwarded to B15's own binder service, which decides everything and owns the row.
    /// The rule's own name says "aggregate" and its sibling
    /// <see cref="The_module_declares_no_organization_aggregate"/> is already scoped this way. The
    /// teeth are kept — and sharpened — by the second half below: a name is a hint, but a persisted
    /// table is the thing that would actually make this module a second owner of money.
    /// </para>
    /// </summary>
    [Fact]
    public void The_module_declares_no_financial_aggregate()
    {
        var forbidden = new[] { "Payment", "Donation", "Wallet", "Transaction", "Invoice", "Refund" };

        var offenders = Module
            .GetTypes()
            .Where(type => type.IsClass
                && !type.IsNested
                && type.Namespace?.Contains(".Domain.", StringComparison.Ordinal) == true)
            .Where(type => forbidden.Any(word =>
                type.Name.Contains(word, StringComparison.Ordinal)))
            .Select(type => type.FullName)
            .ToList();

        offenders.Should().BeEmpty("B15 owns financial truth; B18 expresses business intent");
    }

    /// <summary>
    /// <b>The module persists no financial table.</b>
    /// <para>
    /// The half of the rule above that a rename cannot get past. A class called anything at all
    /// becomes financial state the moment this module maps it, so the check is on what is mapped:
    /// every <c>IEntityTypeConfiguration</c> in this assembly configures a type from this module's
    /// own domain, and nothing from B15's.
    /// </para>
    /// </summary>
    [Fact]
    public void The_module_configures_no_financial_entity()
    {
        var configured = Module
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .SelectMany(type => type.GetInterfaces())
            .Where(contract => contract.IsGenericType
                && contract.GetGenericTypeDefinition().Name.StartsWith(
                    "IEntityTypeConfiguration", StringComparison.Ordinal))
            .Select(contract => contract.GetGenericArguments()[0])
            .ToList();

        configured.Should().NotBeEmpty("the module maps its own aggregates");

        configured
            .Select(type => type.Namespace ?? string.Empty)
            .Should()
            .OnlyContain(
                @namespace => @namespace.StartsWith(
                    "Diaspora.Modules.CommunityProgramme", StringComparison.Ordinal),
                "B18 maps its own domain and nobody else's — least of all B15's");
    }

    /// <summary>
    /// <b>The module declares no second organization or membership-edge aggregate.</b> B12 owns the
    /// organization and the <c>(member, organization, role)</c> triple, and B18's capability set is a
    /// projection of it.
    /// </summary>
    [Fact]
    public void The_module_declares_no_organization_aggregate()
    {
        var offenders = Module
            .GetTypes()
            .Where(type => type.IsClass
                && type.Namespace?.Contains(".Domain.", StringComparison.Ordinal) == true)
            .Where(type => type.Name is "Organization" or "OrganizationMembership")
            .Select(type => type.FullName)
            .ToList();

        offenders.Should().BeEmpty("the organization and its role edge are B12's");
    }

    // ── The URL space ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>One prefix, declared once.</b> A second surface elsewhere in the URL space would be a
    /// contract nobody agreed to.
    /// </summary>
    [Fact]
    public void The_module_declares_exactly_one_route_prefix()
    {
        CommunityProgrammeModuleMetadata.RoutePrefix.Should().Be("/api/v1/communities");

        var literals = SourceTree.ProductionFiles
            .Where(file => file.Path.Contains(
                "Diaspora.Modules.CommunityProgramme", StringComparison.Ordinal))
            .SelectMany(file => file.Root.ToFullString().Split('"'))
            .Where(part => part.StartsWith("/api/v1/", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        literals.Should().OnlyContain(
            literal => literal.StartsWith("/api/v1/communities", StringComparison.Ordinal),
            "every route this module maps sits under its one prefix");
    }

    /// <summary>The module's schema is its own, and it claims no table outside it.</summary>
    [Fact]
    public void The_module_owns_exactly_one_schema()
    {
        CommunityProgrammeModuleMetadata.Schema.Should().Be("community_programme");

        var tables = PlatformModel.Value
            .GetEntityTypes()
            .Where(entity => entity.ClrType.Namespace?.StartsWith(
                "Diaspora.Modules.CommunityProgramme", StringComparison.Ordinal) == true)
            .ToList();

        tables.Should().NotBeEmpty("the module contributes tables");

        tables.Should().OnlyContain(
            entity => entity.GetSchema() == CommunityProgrammeModuleMetadata.Schema,
            "a module owns its own schema and no other");
    }

    // ── The contract's structural promises ───────────────────────────────────────────────────

    /// <summary>
    /// <b>A poll option carries no count, and that absence is the contract.</b> The register: "a
    /// component holding a poll has nothing to add up", which is what makes server-computed results
    /// structurally true rather than a convention somebody could break. A field added here would
    /// break it silently, and this is what notices.
    /// </summary>
    [Fact]
    public void A_poll_option_carries_no_vote_count()
    {
        var counting = typeof(PollOption)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property =>
                property.Name.Contains("Count", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Votes", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Percentage", StringComparison.OrdinalIgnoreCase))
            .Select(property => property.Name)
            .ToList();

        counting.Should().BeEmpty(
            "counts live on the result read alone, and a poll object must have nothing to add up");
    }

    /// <summary>
    /// <b>The poll aggregate carries no per-option tally either.</b> The same rule one level up: a
    /// dictionary on the poll would be a count a mapper could leak.
    /// </summary>
    [Fact]
    public void The_poll_aggregate_carries_only_a_response_total()
    {
        // Every numeric member, whatever it is called. Matching on names would let the next tally
        // through by being spelled differently, and the rule is about arithmetic rather than
        // vocabulary: an aggregate a client holds must carry nothing it can add up per option.
        var numeric = typeof(CommunityPoll)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property =>
                property.PropertyType == typeof(int)
                || property.PropertyType == typeof(int?)
                || property.PropertyType == typeof(long)
                || property.PropertyType == typeof(decimal))
            .Select(property => property.Name)
            .ToList();

        numeric.Should().BeEquivalentTo(
            ["TotalResponses", "MaxSelections", "Revision"],
            "TotalResponses is a total and deliberately not a breakdown; MaxSelections is a rule "
            + "and Revision is the concurrency token. A fourth number would be a tally.");
    }

    // ── Registration ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Every contributor is sealed and constructible without the container.</b> DSP5005's rule: the
    /// kernel instantiates these before a provider exists.
    /// </summary>
    [Fact]
    public void Every_contributor_is_sealed_and_parameterless()
    {
        var contributors = Module
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => type.Name.EndsWith("Contributor", StringComparison.Ordinal))
            .ToList();

        contributors.Should().NotBeEmpty("the module registers through contributor interfaces");

        contributors.Should().OnlyContain(type => type.IsSealed);

        contributors.Should().OnlyContain(
            type => type.GetConstructor(Type.EmptyTypes) != null,
            "the kernel builds these before a provider exists");
    }

    /// <summary>
    /// <b>Both sweeps expose a public <c>RunOnceAsync</c>.</b> Not a convenience: a test that asserted
    /// on a sweep's side effect after a timer happened to fire would be a flaky test wearing a passing
    /// badge, and DEBT-049 is what happens when a loop cannot be driven deterministically.
    /// </summary>
    [Fact]
    public void Every_sweep_can_be_driven_once()
    {
        var sweeps = Module
            .GetTypes()
            .Where(type => type.Name.EndsWith("Sweep", StringComparison.Ordinal))
            .ToList();

        sweeps.Should().NotBeEmpty();

        sweeps.Should().OnlyContain(
            type => type.GetMethod("RunOnceAsync", BindingFlags.Public | BindingFlags.Instance)
                != null);
    }
}
