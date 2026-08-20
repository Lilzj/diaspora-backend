using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// The same host, with campaign review left on.
/// <para>
/// <b>A separate host because the default one turns the gate off to be able to test anything behind
/// it.</b> Publishing under review is a staff decision and this suite has no staff token, so a suite
/// that left the gate on could create a campaign and never publish one — and every transparency test
/// downstream would have nothing to run against. Turning it off globally and never asserting the gate
/// would be worse: the register asks for the review, and a switch nothing tests is a switch that
/// stops working.
/// </para>
/// <para>
/// The contributor set is identical to the default host's, so the two share one EF model cache
/// safely — the platform's "one model cache per DbContext type" trap bites only when the mapped types
/// differ.
/// </para>
/// </summary>
public sealed class CampaignReviewWebApplicationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Financial:RequireCampaignReview"] = "true"
            }));
    }
}
