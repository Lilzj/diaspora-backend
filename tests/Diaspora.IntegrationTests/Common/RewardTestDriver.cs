using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Rewards;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// Lets time pass for reward points, so a test can spend what a member earned.
/// <para>
/// Granted points are pending until they vest (D-9), a week by default, and pending points cannot be
/// spent (Q3). This host has no clock to move, so a test that earns points and then redeems them
/// vests that member's points as the sweep would once the longest window had passed. It is one
/// member's account and nobody else's: the database is shared across a collection.
/// </para>
/// </summary>
public static class RewardTestDriver
{
    public static async Task VestRewardPointsAsync(
        this WebApplicationFactory<Api.Program> factory, Guid memberUserId)
    {
        ArgumentNullException.ThrowIfNull(factory);

        using var scope = factory.Services.CreateScope();

        var accounts = scope.ServiceProvider.GetRequiredService<IRewardAccountRepository>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var account = await accounts.FindByMemberAsync(memberUserId);

        if (account is null
            || account.VestDue(clock.UtcNow.AddDays(RewardVesting.MaximumVestingDays + 1)) == 0)
        {
            return;
        }

        accounts.Update(account);

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }
}
