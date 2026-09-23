using Xunit;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// The Community test classes, over one application and one database.
/// <para>
/// See <see cref="HousingCollection"/> for why collections exist here: building an
/// application costs 32 seconds, and a class that asks for a class fixture gets one of its
/// own. The classes below share a database, so a test that assumes an empty table will find
/// its neighbours’ rows — which is why each collection is proved by running it.
/// </para>
/// </summary>
[CollectionDefinition(Name)]
public sealed class CommunityCollection : ICollectionFixture<CustomWebApplicationFactory>
{
    public const string Name = "Community";
}
