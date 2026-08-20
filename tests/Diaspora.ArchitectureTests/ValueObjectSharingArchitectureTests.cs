using System.Reflection;
using Diaspora.Modules.Community.Persistence;
using Diaspora.Modules.Guides.Persistence;
using Diaspora.Modules.Jobs.Persistence;
using Diaspora.Modules.Location.Persistence;
using Diaspora.Modules.Media.Persistence;
using Diaspora.Modules.Providers.Persistence;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Persistence;
using Diaspora.Persistence.Modules;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// One rule, across every module: <b>a type mapped as an EF owned type must not expose a cached
/// static instance of itself.</b>
/// <para>
/// An owned instance shared between two aggregates in one <c>DbContext</c> is tracked against
/// whichever owner EF saw first. The second owner silently writes nulls into its NOT NULL
/// columns, and the save fails with a constraint violation naming a column the calling code never
/// went near. Nothing about the sharing site reports a problem.
/// </para>
/// <para>
/// <b>It survives ordinary testing.</b> No HTTP request writes two aggregates of the same type in
/// one unit of work, so a whole phase can pass with the defect present. Every bulk import,
/// reference-data seeder, test fixture and future "duplicate this" feature trips it. B9 hit it
/// seeding two thousand postings; the cursor work that followed hit B6's version seeding three
/// hundred posts, on a column — <c>visibility_level</c> — that the fixture never touched.
/// </para>
/// <para>
/// The fix is a per-call property — an expression-bodied member returning a new instance rather
/// than an auto-property initialised once. It costs an allocation nobody will measure and removes
/// the hazard entirely. Structural equality is unaffected: these types compare by value.
/// </para>
/// <para>
/// <b>Owned-ness is read from the EF model rather than guessed from a base type.</b> That is what
/// makes the rule exact — it flags the types where the hazard is real and stays silent on value
/// objects that are never persisted — and it means a type newly mapped as owned is covered the
/// day it is mapped, with no list to remember to update.
/// </para>
/// </summary>
public sealed class ValueObjectSharingArchitectureTests
{
    private readonly ITestOutputHelper _output;

    public ValueObjectSharingArchitectureTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void No_owned_type_exposes_a_cached_static_instance_of_itself()
    {
        var offenders = new List<string>();

        foreach (var type in OwnedClrTypes())
        {
            foreach (var property in type.GetProperties(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (!type.IsAssignableFrom(property.PropertyType))
                {
                    continue;
                }

                // An auto-property initialised once carries a compiler-generated backing field,
                // and therefore one shared instance for the life of the process. An
                // expression-bodied property has no such field and allocates per call.
                var backing = type.GetField(
                    $"<{property.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (backing is not null)
                {
                    offenders.Add($"{type.FullName}.{property.Name}");
                }
            }
        }

        foreach (var offender in offenders)
        {
            _output.WriteLine(offender);
        }

        // Brace-free on purpose: FluentAssertions treats this as a format string, so the very
        // syntax the rule is about would be read as a placeholder and fail the Release build.
        offenders.Should().BeEmpty(
            "a cached owned instance is tracked against one owner only, and the second aggregate "
            + "to reference it writes nulls into NOT NULL columns. Make it an expression-bodied "
            + "property returning a new instance, rather than an auto-property initialised once");
    }

    /// <summary>
    /// Guards the guard. A reflection rule that quietly stops matching anything passes forever,
    /// which is the one failure an architecture test cannot afford. If the model ever comes back
    /// without the modules' owned types, this fails rather than the rule silently going green.
    /// </summary>
    [Fact]
    public void The_rule_actually_sees_the_platforms_owned_types()
    {
        var owned = OwnedClrTypes();

        owned.Should().HaveCountGreaterThan(
            40, "every module maps owned types; finding almost none means the model did not build");

        owned.Select(type => type.Assembly.GetName().Name)
            .Distinct()
            .Should().HaveCountGreaterThan(
                3, "the model must span modules, not just the ones loaded by accident");
    }

    /// <summary>
    /// The CLR types EF maps as owned, read from a real model built with every module's database
    /// contributor.
    /// <para>
    /// <b>B1.3 changed where this list comes from, and it mattered.</b> It used to name seven
    /// contributors by hand, and it still named seven after two more verticals landed — so
    /// Marketplace and Housing were exempt from this rule by omission, silently, from B10 until now.
    /// <see cref="PlatformModel"/> discovers them with the same scanner the host uses, which means
    /// the module that lands next is covered without anybody remembering to add it.
    /// </para>
    /// </summary>
    private static List<Type> OwnedClrTypes() =>
        PlatformModel.Value.GetEntityTypes()
            .Where(entity => entity.IsOwned())
            .Select(entity => entity.ClrType)
            .Distinct()
            .ToList();
}
