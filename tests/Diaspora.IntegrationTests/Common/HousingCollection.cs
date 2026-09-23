using Xunit;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// The housing test classes, over one application and one database.
/// <para>
/// <b>Why a collection exists at all.</b> Every test class that asks for a class fixture gets an
/// application of its own, and building one costs <b>32 seconds</b> — measured on 23 Sep 2026,
/// against 4 seconds for the schema and 7 for the reference data. With 123 classes that is
/// roughly 3,900 processor-seconds spent before any test body runs, and with eight applications
/// building at once on an eight-processor machine it is where the suite's timeouts came from:
/// fifteen to twenty-four failures a run, all <c>TaskCanceledException</c>, never an assertion,
/// a different set each time.
/// </para>
/// <para>
/// <b>What joining one costs.</b> The classes in a collection share a database, so a test that
/// assumes an empty table will find its neighbours' rows. Housing was chosen first because its ten
/// classes already tag their data — a run identifier in the title, a landlord created per test —
/// and because they have a measured baseline to compare against: 8m50s for 94 tests in isolation.
/// </para>
/// <para>
/// xUnit runs a collection's classes one after another, so this trades parallelism for not
/// building ten applications. That is the right trade here only because the building dominates:
/// the tests themselves run in about a second each.
/// </para>
/// </summary>
[CollectionDefinition(Name)]
public sealed class HousingCollection : ICollectionFixture<CustomWebApplicationFactory>
{
    public const string Name = "Housing";
}
