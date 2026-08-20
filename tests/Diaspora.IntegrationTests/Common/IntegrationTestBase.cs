using Xunit;

namespace Diaspora.IntegrationTests.Common;

public abstract class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    protected HttpClient Client { get; }
    protected CustomWebApplicationFactory Factory { get; }

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    public void Dispose()
    {
        Client.Dispose();
        GC.SuppressFinalize(this);
    }
}
