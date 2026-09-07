using Diaspora.Modules.Financial.Domain.Withdrawals;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Diaspora.Payments.Stripe;

/// <summary>
/// Finds a member's connected account on their own payout destinations.
/// <para>
/// <b>The mapping was already stored; nothing read it.</b> <c>PayoutDestination.ProviderToken</c>
/// holds the provider's handle for a destination, and for Stripe that handle <em>is</em> the
/// connected account. The only <see cref="IStripeAccountResolver"/> that shipped was
/// <c>UnmappedStripeAccountResolver</c>, which answers null for everybody — so
/// <see cref="StripeConnectVerificationSource"/> reported <c>NotStarted</c> for every member on the
/// platform, and every payout route requiring onboarding refused.
/// </para>
/// <para>
/// <b>Matched on provider, not on kind.</b> A member can hold a Flutterwave destination and a
/// Stripe one at once, and handing a Flutterwave token to Stripe would ask Stripe about an account
/// it has never heard of — which reads as "not onboarded" and refuses a payout that would have
/// worked.
/// </para>
/// <para>
/// <b>It opens its own scope, and that is not incidental.</b> The verification source that consumes
/// this is a singleton, and the destination repository is scoped to a request because it is
/// EF's. Injecting the repository directly would capture one request's <c>DbContext</c> in a
/// singleton for the lifetime of the process — the classic captive dependency, which fails at the
/// first concurrent call rather than at startup.
/// </para>
/// </summary>
public sealed class StripeDestinationAccountResolver : IStripeAccountResolver
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptionsMonitor<StripeOptions> _options;

    public StripeDestinationAccountResolver(
        IServiceScopeFactory scopes, IOptionsMonitor<StripeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(options);

        _scopes = scopes;
        _options = options;
    }

    public async Task<string?> ResolveAsync(
        Guid payeeUserId, CancellationToken cancellationToken = default)
    {
        var provider = _options.CurrentValue.Provider;

        await using var scope = _scopes.CreateAsyncScope();

        var destinations = await scope.ServiceProvider
            .GetRequiredService<IPayoutDestinationRepository>()
            .ListForUserAsync(payeeUserId, includeRemoved: false, cancellationToken);

        return destinations
            .Where(destination =>
                string.Equals(destination.Provider, provider, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(destination.ProviderToken))

            // The verified one first where there is one: a member part-way through opening a second
            // account still has a first that Stripe will pay to, and asking about the unfinished one
            // would refuse a withdrawal they can make today.
            .OrderByDescending(destination => destination.IsVerified)
            .ThenByDescending(destination => destination.CreatedAt)
            .Select(destination => destination.ProviderToken)
            .FirstOrDefault();
    }
}
