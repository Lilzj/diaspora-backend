using System.Reflection;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Events;
using Diaspora.Modules.Analytics;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Events;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Domain.Rollups;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// B19's boundaries, asserted structurally.
/// <para>
/// <b>Every rule here is one the phase brief states and prose cannot keep.</b> "Analytics is derived
/// data", "do not duplicate domain truth", "no personally identifiable information", "business
/// metrics originate from trusted domain events" — each of those survives exactly as long as the
/// next contributor remembers it, unless something fails the build.
/// </para>
/// </summary>
public sealed class AnalyticsArchitectureTests
{
    private static readonly Assembly Analytics = typeof(AnalyticsModule).Assembly;

    /// <summary>
    /// <b>The module references no other module, and this is the phase's central architectural
    /// claim.</b> Every fact it aggregates arrives as a published contract. A module reference would
    /// let analytics read another module's <em>current</em> state — and analytics that can do that
    /// has stopped being a record of what happened and become a second answer to what is true now,
    /// which is the failure §4 of the brief forbids by name.
    /// </summary>
    [Fact]
    public void The_analytics_module_references_no_other_module()
    {
        var offenders = Analytics
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("Diaspora.Modules.", StringComparison.Ordinal))
            .Where(name => !string.Equals(name, Analytics.GetName().Name, StringComparison.Ordinal))
            .ToList();

        offenders.Should().BeEmpty(
            "analytics consumes published contracts and nothing else:\n" + string.Join("\n", offenders));
    }

    /// <summary>
    /// <b>Two URL spaces, both versioned, both one segment deep, and neither is another module's.</b>
    /// The Phase 7 register writes the creator analytics routes into B18's space; B18 owns it, an
    /// architecture rule says so, and the Phase 15 register — written later over the same capability
    /// — puts them here instead.
    /// </summary>
    [Fact]
    public void The_module_declares_exactly_two_route_prefixes_and_owns_both()
    {
        AnalyticsModuleMetadata.AnalyticsRoutePrefix.Should().Be("/api/v1/analytics");
        AnalyticsModuleMetadata.TelemetryRoutePrefix.Should().Be("/api/v1/telemetry");

        typeof(AnalyticsModuleMetadata)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Count(field => field.Name.EndsWith("RoutePrefix", StringComparison.Ordinal))
            .Should().Be(2, "a third URL space would be one no rule about prefixes had considered");

        AnalyticsModuleMetadata.AnalyticsRoutePrefix.Should().NotStartWith(
            "/api/v1/creators", "the creator projection's URL space is B18's");
    }

    /// <summary>
    /// <b>One consumer per published contract, and B8's comment relay is not one of them.</b> B8
    /// relays Community's comment event for guide-focused consumers and its own contract says
    /// plainly: "a consumer that already listens to Community's stream should not also subscribe
    /// here, or it counts every comment twice." This module listens to Community's stream.
    /// </summary>
    [Fact]
    public void No_published_contract_is_consumed_twice_and_the_comment_relay_is_not_consumed()
    {
        var consumed = Analytics
            .GetTypes()
            .Select(type => type.BaseType)
            .Where(baseType => baseType is { IsGenericType: true }
                && baseType.GetGenericTypeDefinition() == typeof(IntegrationEventHandler<>))
            .Select(baseType => baseType!.GetGenericArguments()[0])
            .ToList();

        consumed.Should().NotBeEmpty("the module is a consumer before it is anything else");

        consumed.Should().OnlyHaveUniqueItems(
            "two consumers of one contract would count every fact twice");

        consumed.Select(type => type.Name).Should().NotContain(
            "GuideCommentAddedIntegrationEvent",
            "B8's relay duplicates Community's comment stream, which this module already consumes");

        foreach (var contract in consumed)
        {
            typeof(IIntegrationEvent).IsAssignableFrom(contract).Should().BeTrue(
                "{0} must be a published contract", contract.Name);
        }
    }

    /// <summary>
    /// <b>No stored analytics row has a field that names a person.</b> §33 and §47 forbid a
    /// readership record; the enforcement is that there is nowhere to put one. An actor is a keyed
    /// hash, an owner is the subject of the caller's own query, and nothing else on the row is an
    /// identity.
    /// </summary>
    [Fact]
    public void No_analytics_row_carries_a_field_that_names_a_person()
    {
        var forbidden = new[]
        {
            "Email", "Phone", "DisplayName", "Handle", "FullName", "Address", "Ip", "IpAddress",
            "DeviceId", "UserAgent", "Latitude", "Longitude", "Query", "SearchTerm", "Body", "Content"
        };

        foreach (var stored in new[]
        {
            typeof(AnalyticsEvent),
            typeof(ContentDailyRollup),
            typeof(OwnerDailyRollup),
            typeof(Modules.Analytics.Domain.Dimensions.AnalyticsContentDimension)
        })
        {
            var offenders = stored
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .Where(name => forbidden.Any(term =>
                    name.Equals(term, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            offenders.Should().BeEmpty(
                "{0} must carry no personal field, and found: {1}",
                stored.Name,
                string.Join(", ", offenders));
        }
    }

    /// <summary>
    /// <b>The actor is stored as a key and never as an identifier.</b> A <c>Guid ActorUserId</c> on
    /// the raw row would make the analytics store able to name who read what — which is precisely the
    /// per-viewer record the register says must not exist "at any level of authorization short of a
    /// legal process".
    /// </summary>
    [Fact]
    public void The_raw_row_stores_an_actor_key_rather_than_an_actor()
    {
        var actor = typeof(AnalyticsEvent).GetProperty("ActorKey");

        actor.Should().NotBeNull();
        actor!.PropertyType.Should().Be<string>("a hash, not an identifier");

        typeof(AnalyticsEvent)
            .GetProperties()
            .Should().NotContain(property => property.Name == "ActorUserId");
    }

    /// <summary>
    /// <b>Only three event types may be submitted by a client, and none of them is a business
    /// metric.</b> §41: a device may report that somebody looked at something; it may not report
    /// that somebody saved, shared, commented, applied, attended or paid.
    /// </summary>
    [Fact]
    public void Only_observation_events_are_client_submittable()
    {
        var clientSubmittable = new[]
        {
            AnalyticsEventTypes.ContentViewed,
            AnalyticsEventTypes.ProfileViewed,
            AnalyticsEventTypes.ContentImpression
        };

        clientSubmittable.Should().OnlyContain(name => name.Contains("view", StringComparison.Ordinal)
            || name.Contains("impression", StringComparison.Ordinal));

        var serverOnly = new[]
        {
            AnalyticsEventTypes.ContentSaved,
            AnalyticsEventTypes.ContentShared,
            AnalyticsEventTypes.ContentCommented,
            AnalyticsEventTypes.ContentReacted,
            AnalyticsEventTypes.ContentConverted,
            AnalyticsEventTypes.ContentPublished,
            AnalyticsEventTypes.AudienceFollowed,
            AnalyticsEventTypes.AudienceUnfollowed
        };

        serverOnly.Should().NotIntersectWith(clientSubmittable);
    }

    /// <summary>
    /// <b>Every summable figure is on the counts record, and nothing that is not summable is.</b> A
    /// distinct count or a rate on this type would be added across days by the only code path that
    /// reads it, and the result would be published under the correct name with the wrong arithmetic.
    /// </summary>
    [Fact]
    public void The_rollup_counts_carry_only_additive_figures()
    {
        var forbidden = new[] { "Unique", "Rate", "Average", "Median", "Percent", "Distinct" };

        var offenders = typeof(AnalyticsCounts)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.Name != nameof(AnalyticsCounts.Engagements))
            .Select(property => property.Name)
            .Where(name => forbidden.Any(term => name.Contains(term, StringComparison.Ordinal)))
            .ToList();

        offenders.Should().BeEmpty(
            "a figure that cannot be summed must not live on a record that is:\n"
                + string.Join("\n", offenders));
    }

    /// <summary>
    /// <b>Nothing in this module mutates a domain aggregate or writes money.</b> §4: analytics
    /// consumes and never commands. Asserted over the source because the absence of a call is not
    /// visible in metadata.
    /// </summary>
    [Fact]
    public void The_module_names_no_financial_or_domain_write()
    {
        var forbidden = new[]
        {
            "IRewardPointsAccount",
            "IWallet",
            "PaymentIntent",
            "LedgerEntry",
            "INotificationService",
            "IRecommendationEngine"
        };

        var offenders = new List<string>();

        foreach (var file in SourceTree.ProductionFiles
            .Where(file => file.Path.Contains("Diaspora.Modules.Analytics", StringComparison.Ordinal)))
        {
            var text = file.Root.ToFullString();

            offenders.AddRange(forbidden
                .Where(term => text.Contains(term, StringComparison.Ordinal))
                .Select(term => $"{file.Path} — {term}"));
        }

        offenders.Should().BeEmpty(
            "analytics owns the interpretation, never the truth:\n" + string.Join("\n", offenders));
    }

    /// <summary>
    /// <b>Every metric this deployment publishes has a definition, and every definition names an
    /// owner.</b> §35 and FRONTEND-PHASE15-GAP-030: two definitions of one word is the failure, and
    /// the catalogue is the single source only if it is complete.
    /// </summary>
    [Fact]
    public void Every_metric_has_a_definition_a_formula_where_one_is_meaningful_and_an_owner()
    {
        AnalyticsMetricCatalog.Definitions.Should().NotBeEmpty();

        AnalyticsMetricCatalog.Definitions
            .Select(definition => definition.Key)
            .Should().OnlyHaveUniqueItems();

        AnalyticsMetricCatalog.Definitions
            .Select(definition => definition.CatalogueKey)
            .Should().OnlyHaveUniqueItems("a Phase 15 key must name one metric");

        foreach (var definition in AnalyticsMetricCatalog.Definitions
            .Where(definition => definition.Format == AnalyticsMetricFormat.Percentage
                && definition.Availability == AnalyticsMetricAvailability.Available))
        {
            definition.Formula.Should().NotBeNullOrWhiteSpace(
                "{0} is a rate, and §16 forbids an unexplained percentage", definition.Key);
        }
    }

    /// <summary>
    /// <b>An owner is a member, never the profile a module keeps for them.</b> 13 Sep 2026.
    /// <para>
    /// Six consumers wrote <c>SellerId</c>, <c>LandlordId</c> and <c>EmployerId</c> — each module's
    /// own profile identifier, naming no user — into <c>OwnerUserId</c> and <c>ActorUserId</c>, and
    /// into the owner a listing's dimension is recorded against. Every marketplace, housing and jobs
    /// row in production was keyed by an id no owner query ever asks for, so a seller's dashboard
    /// could only ever be empty. Nothing failed: 5,411 client tests and 4,997 backend tests passed,
    /// because every fixture used one Guid for both the seller and the member. It was found by
    /// joining the rollup to the users table on the device's data and getting no names back.
    /// </para>
    /// <para>
    /// One consumer had half-fixed it — <c>EmployerUserId ?? EmployerId</c> — which put the right id
    /// first and the wrong one behind it. That fallback is rejected here too.
    /// </para>
    /// </summary>
    [Fact]
    public void Analytics_consumers_never_record_a_profile_id_as_an_owner()
    {
        // The profile identifiers this rule was written against. Each has a *UserId sibling on
        // its event, or none at all — in which case the fact must send no owner and let ingestion
        // ask the dimension.
        var profileIds = new[] { "SellerId", "LandlordId", "EmployerId" };

        bool NamesAProfile(ExpressionSyntax expression)
        {
            var text = new string(expression.ToString().Where(ch => !char.IsWhiteSpace(ch)).ToArray());

            return profileIds.Any(id =>
                text.EndsWith("." + id, StringComparison.Ordinal)
                || text.Contains("." + id + "??", StringComparison.Ordinal)
                || text.Contains("??integrationEvent." + id, StringComparison.Ordinal));
        }

        var offenders = new List<string>();

        foreach (var file in SourceTree.ProductionFiles
            .Where(file => file.Path.Contains("Diaspora.Modules.Analytics/Application/Consumers", StringComparison.Ordinal)))
        {
            // `new AnalyticsFact(..., OwnerUserId: x, ActorUserId: y)`
            foreach (var creation in file.Root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                var typeName = creation is ObjectCreationExpressionSyntax named ? named.Type.ToString() : string.Empty;

                if (!typeName.EndsWith("AnalyticsFact", StringComparison.Ordinal) || creation.ArgumentList is null)
                {
                    continue;
                }

                foreach (var argument in creation.ArgumentList.Arguments)
                {
                    var name = argument.NameColon?.Name.Identifier.Text;

                    if (name is "OwnerUserId" or "ActorUserId" && NamesAProfile(argument.Expression))
                    {
                        offenders.Add($"{file.Locate(argument)} — {name}: {argument.Expression}");
                    }
                }
            }

            // `_dimensions.ObserveAsync(type, contentId, owner, ...)` — the owner is the third argument.
            foreach (var invocation in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: "ObserveAsync" }
                    || invocation.ArgumentList.Arguments.Count < 3)
                {
                    continue;
                }

                var owner = invocation.ArgumentList.Arguments[2];

                if (NamesAProfile(owner.Expression))
                {
                    offenders.Add($"{file.Locate(owner)} — ObserveAsync owner: {owner.Expression}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "an owner column holds a member's user id; a profile id there keys the row to an id no "
            + "owner query asks for:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }
}
