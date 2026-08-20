using Diaspora.Persistence;
using Diaspora.Persistence.Atomic;
using Diaspora.Persistence.Modules;
using Diaspora.Platform.Modules;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;
using Xunit.Abstractions;

namespace Diaspora.UnitTests.Persistence.Atomic;

/// <summary>
/// The same guarded update, generated for each provider, asserted rather than assumed.
/// <para>
/// <b>A compatibility claim nobody executed is a compatibility claim.</b> The integration suite runs
/// on SQLite and production runs on PostgreSQL, so "it works on both" was going to rest on one of
/// them being exercised and the other being believed. Generating the statement for each provider's
/// own <see cref="ISqlGenerationHelper"/> costs no connection — SQL generation is a function of the
/// model — and turns the claim into output somebody can read.
/// </para>
/// </summary>
public sealed class AtomicProviderCompatibilityTests
{
    private static readonly IReadOnlyList<IDatabaseContributor> Contributors =
        [new AtomicTestDatabaseContributor()];

    private readonly ITestOutputHelper _output;

    public AtomicProviderCompatibilityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData("PostgreSQL")]
    [InlineData("SQLite")]
    public void The_reservation_guard_generates_a_statement_for_every_provider(string provider)
    {
        var statement = Reservation(provider);

        _output.WriteLine($"── {provider} ──\n{statement.Sql}\n");

        statement.Sql.Should().StartWith("UPDATE ");
        statement.Sql.Should().Contain("units_held");
        statement.Sql.Should().Contain("units_total");
        statement.Sql.Should().Contain("units_consumed");
        statement.Sql.Should().Contain(">=", "the capacity check is the whole point");

        statement.Parameters.Should().HaveCount(
            3, "the key, the delta in the assignment, and the delta in the guard");
    }

    /// <summary>
    /// The clamped forms are the ones most likely to reach for a provider-specific function —
    /// <c>GREATEST</c> on PostgreSQL, <c>MAX</c> on SQLite, neither on both. They use
    /// <c>CASE WHEN</c>, which is standard, and this asserts they still do.
    /// </summary>
    [Theory]
    [InlineData("PostgreSQL")]
    [InlineData("SQLite")]
    public void The_clamped_forms_use_standard_sql_rather_than_a_provider_function(string provider)
    {
        var statement = Consume(provider);

        _output.WriteLine($"── {provider} ──\n{statement.Sql}\n");

        statement.Sql.Should().Contain("CASE WHEN");
        statement.Sql.Should().NotContainAny("GREATEST", "LEAST", "IIF", "IFNULL", "COALESCE");
    }

    /// <summary>
    /// Every value is a parameter and every identifier came from the model through the provider's
    /// own delimiter. Nothing a caller supplies is ever concatenated into the text, which is what
    /// makes the injection question answerable rather than argued.
    /// </summary>
    [Theory]
    [InlineData("PostgreSQL")]
    [InlineData("SQLite")]
    public void No_caller_supplied_value_reaches_the_statement_text(string provider)
    {
        var statement = Reservation(provider);

        statement.Sql.Should().NotContain(
            "7", "the unit count is bound, not written into the text");

        statement.Parameters.Select(parameter => parameter.Value)
            .Should().Contain(value => Equals(value, 7L));
    }

    private static AtomicStatement Reservation(string provider)
    {
        using var context = Context(provider);

        return AtomicStatement.Update(
            context.Model,
            context.GetService<ISqlGenerationHelper>(),
            AtomicUpdate.For<AtomicTestResource>(AtomicTestResourceId.New())
                .Increment(resource => resource.Capacity.Held, 7)
                .Guard(term => term
                    .Column(resource => resource.Capacity.Total)
                    .Minus(resource => resource.Capacity.Held)
                    .Minus(resource => resource.Capacity.Consumed)
                    .AtLeast(7)));
    }

    private static AtomicStatement Consume(string provider)
    {
        using var context = Context(provider);

        return AtomicStatement.Update(
            context.Model,
            context.GetService<ISqlGenerationHelper>(),
            AtomicUpdate.For<AtomicTestResource>(AtomicTestResourceId.New())
                .DecrementToZero(resource => resource.Capacity.Held, 2)
                .IncrementLimited(
                    resource => resource.Capacity.Consumed, 2, resource => resource.Capacity.Held));
    }

    /// <summary>
    /// A context on the named provider. No connection is opened: building a model and asking for a
    /// SQL generation helper need a provider, not a database.
    /// </summary>
    private static DiasporaDbContext Context(string provider)
    {
        var builder = new DbContextOptionsBuilder<DiasporaDbContext>()
            .ReplaceService<IModelCacheKeyFactory, AtomicTestModelCacheKeyFactory>();

        _ = provider switch
        {
            "PostgreSQL" => builder.UseNpgsql("Host=localhost;Database=unused"),
            "SQLite" => builder.UseSqlite("Data Source=:memory:"),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown provider.")
        };

        return new DiasporaDbContext(
            builder.Options, currentUser: null, clock: null, eventDispatcher: null, Contributors);
    }
}
