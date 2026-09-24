using Xunit;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// The Platform test classes, over one application and one database.
/// <para>
/// See <see cref="HousingCollection"/> for why collections exist here: building an application
/// costs 32 seconds, and a class that asks for a class fixture gets one of its own. Pooling pays
/// where that beats the parallelism it gives up, which is to say where a class has few tests —
/// Community, at 23 tests a class, gained nothing.
/// </para>
/// <para>
/// The classes below share a database, so a test that assumes an empty table will find its
/// neighbours' rows. Each collection is proved by running it against a baseline.
/// </para>
/// </summary>
[CollectionDefinition(Name)]
public sealed class PlatformCollection : ICollectionFixture<CustomWebApplicationFactory>
{
    public const string Name = "Platform";
}
