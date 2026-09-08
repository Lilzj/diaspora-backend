using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Withdrawals;
using Diaspora.Modules.Financial.Extensibility.Payouts;
using Diaspora.Modules.Financial.Infrastructure.Payouts;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Withdrawals;

/// <summary>
/// Getting a member from "no payout account" to one the platform will pay to.
///
/// <para>
/// <b>The gap these close.</b> Registering a destination and verifying one both existed and nothing
/// joined them. No screen in the application could add a destination at all — the client's
/// <c>registerPayoutDestination</c> and its hook were written and never called — and a destination,
/// once added, arrived unverified with only a staff route out of it. Every member had zero
/// destinations, so the withdraw screen's "you have not added an account to withdraw to yet" was
/// the end of the road for everybody.
/// </para>
///
/// <para>
/// <b>No credential passes through this platform.</b> The member finishes on the provider's own
/// pages and what comes back is an account reference. That is why onboarding answers a URL rather
/// than accepting a form.
/// </para>
/// </summary>
public sealed class PayoutOnboardingHandlers
    : ICommandHandler<StartPayoutOnboardingCommand, PayoutOnboardingResponse>,
        ICommandHandler<RegisterPayoutIdentityCommand, PayoutDestinationResponse>,
        ICommandHandler<RefreshPayoutDestinationCommand, PayoutDestinationResponse>
{
    /// <summary>
    /// Where the provider returns a member the client did not name a destination for.
    /// <para>
    /// A deep link rather than a web page, because the member started in the application and is
    /// coming back to it. A provider that will not accept a custom scheme refuses the link and says
    /// so, which is better than sending somebody to a page that does not exist.
    /// </para>
    /// </summary>
    private const string DefaultReturnUrl = "diaspora://wallet/payout-accounts";

    private readonly IPayoutDestinationRepository _destinations;
    private readonly PayoutProviderRouter _router;
    private readonly IEnumerable<IPayoutOnboarding> _onboarding;
    private readonly IEnumerable<IPayoutProviderVerificationSource> _verification;
    private readonly FinancialIdentityService _identities;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public PayoutOnboardingHandlers(
        IPayoutDestinationRepository destinations,
        PayoutProviderRouter router,
        IEnumerable<IPayoutOnboarding> onboarding,
        IEnumerable<IPayoutProviderVerificationSource> verification,
        FinancialIdentityService identities,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _destinations = destinations;
        _router = router;
        _onboarding = onboarding;
        _verification = verification;
        _identities = identities;
        _authorization = authorization;
        _currencies = currencies;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    // ── Starting ────────────────────────────────────────────────────────────────────────────

    public async Task<Result<PayoutOnboardingResponse>> Handle(
        StartPayoutOnboardingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<PayoutOnboardingResponse>(caller.Error);
        }

        var country = command.CountryCode.Trim().ToUpperInvariant();

        // **The matrix decides which of the two shapes this country needs, before any provider is
        // called.** A route that requires provider onboarding is a hand-off; one that does not is an
        // address the member already has. Asking Stripe to open an account for somebody who will be
        // paid through PayPal would create a connected account nothing ever uses.
        var routes = _router.RoutesInto(country);
        var identity = routes.FirstOrDefault(route => !route.RequiresProviderOnboarding);

        if (identity is not null)
        {
            return Result.Success(new PayoutOnboardingResponse
            {
                Started = false,
                RequiresIdentity = true,
                Provider = identity.Provider,
                IdentityHint = HintFor(identity.Provider),
            });
        }

        var adapter = _onboarding.FirstOrDefault();

        if (adapter is null)
        {
            // Not an error the member caused, and named as the deployment's own so support does not
            // go looking at their account.
            return Result.Failure<PayoutOnboardingResponse>(Error.Conflict(
                FinancialErrorCodes.PayoutNotSupported,
                "This deployment cannot set up payout accounts yet."));
        }

        // **The matrix is asked before the provider is, and the first cut of this did not ask it
        // at all.**
        //
        // It handed whatever country the client sent straight to Stripe, so a member in Mauritius
        // — where Stripe publishes no payout corridor — got Stripe's own words about a Connect
        // signup, which is a problem they cannot act on and is not the reason they cannot be paid.
        // `Financial:PayoutRouting` answers this, and refusing here also means not opening an
        // account at a provider that will never be able to use it.
        if (routes.Count == 0)
        {
            var supported = _router.SupportedCountries();

            return Result.Success(new PayoutOnboardingResponse
            {
                Started = false,
                Provider = adapter.Provider,
                Reason = supported.Count == 0
                    ? "Diaspora cannot send money out of the platform from this deployment yet."
                    : $"Diaspora cannot pay out to {country} yet. Payouts currently reach "
                        + $"{string.Join(", ", supported)} accounts only.",
            });
        }

        var facts = await _currencies.FactsAsync(command.Currency, cancellationToken);

        var mine = await _destinations.ListForUserAsync(
            caller.Value, includeRemoved: false, cancellationToken);

        // **Resumed, not reopened.** A member who abandoned the provider's form comes back to the
        // account they already have; opening a second one for the same person is a support
        // conversation nobody can unpick from this side.
        var existing = mine.FirstOrDefault(destination =>
            string.Equals(destination.Provider, adapter.Provider, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(destination.ProviderToken));

        var session = await adapter.StartAsync(
            new PayoutOnboardingRequest
            {
                PayeeUserId = caller.Value,
                CountryCode = country,
                Currency = facts.Code,
                ExistingAccountReference = existing?.ProviderToken,
                ReturnUrl = command.ReturnUrl ?? DefaultReturnUrl,

                // The same place: a link that has expired sends the member back to the screen that
                // asks for a new one, which is where they already were.
                RefreshUrl = command.ReturnUrl ?? DefaultReturnUrl,
            },
            cancellationToken);

        // **The account is recorded even when the link fails.** It exists at the provider from the
        // moment onboarding opens, and dropping the reference here would strand it — the next
        // attempt would open another.
        var destination = existing;

        if (destination is null && session.AccountReference is { } reference)
        {
            destination = await OpenDestinationAsync(
                caller.Value, adapter.Provider, reference, facts.Code, country,
                cancellationToken);
        }

        if (!session.Started)
        {
            return Result.Success(new PayoutOnboardingResponse
            {
                Started = false,
                Provider = adapter.Provider,
                DestinationId = destination?.Id.Value,
                Reason = session.FailureMessage ?? "That could not be set up right now.",
            });
        }

        return Result.Success(new PayoutOnboardingResponse
        {
            Started = true,
            Url = session.Url,
            Provider = adapter.Provider,
            DestinationId = destination?.Id.Value,
        });
    }

    // ── An address rather than a setup page ───────────────────────────────────

    public async Task<Result<PayoutDestinationResponse>> Handle(
        RegisterPayoutIdentityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<PayoutDestinationResponse>(caller.Error);
        }

        var identity = command.Identity.Trim();

        if (identity.Length == 0)
        {
            return Result.Failure<PayoutDestinationResponse>(Error.Validation(
                FinancialErrorCodes.AmountInvalid,
                "Enter the address your payouts should go to."));
        }

        var country = command.CountryCode.Trim().ToUpperInvariant();

        // **Which provider this is registered against is the matrix's answer, never the client's.**
        // The shipped RegisterPayoutDestination route lets a caller name any provider and any token,
        // which is a client-supplied claim about where somebody's money goes. Here the country
        // decides, and a country with no address-shaped route has nothing to register.
        var route = _router.RoutesInto(country)
            .FirstOrDefault(candidate => !candidate.RequiresProviderOnboarding);

        if (route is null)
        {
            return Result.Failure<PayoutDestinationResponse>(Error.Conflict(
                FinancialErrorCodes.PayoutCountryUnsupported,
                $"Diaspora cannot pay out to {country} this way yet."));
        }

        var facts = await _currencies.FactsAsync(command.Currency, cancellationToken);

        var account = await _identities.GetOrCreateAsync(
            caller.Value,
            FinancialAccountType.Personal,
            facts.Code,
            countryCode: country,
            cancellationToken: cancellationToken);

        var destination = PayoutDestination.Register(
            PayoutDestinationId.From(_guids.NewSequentialGuid()),
            caller.Value,
            account.Id,
            PayoutDestinationKind.ProviderManaged,
            Mask(route.Provider, identity),
            facts.Code,
            _clock.UtcNow,
            route.Provider,
            identity,
            country);

        _destinations.Add(destination);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FinancialMapperExtensions.ToResponse(destination));
    }

    /// <summary>
    /// What to ask for, in the member's words.
    /// <para>
    /// Named per provider because "your payout address" means nothing to anybody. A provider with no
    /// phrasing here still works — the client falls back — but it reads worse, so adding one is
    /// part of adding an adapter.
    /// </para>
    /// </summary>
    private static string HintFor(string provider) => provider.ToLowerInvariant() switch
    {
        "paypal" => "your PayPal email address",
        "flutterwave" => "your mobile money number",
        _ => "the address your payouts should go to",
    };

    /// <summary>
    /// The label a member reads, produced here rather than accepted from a client.
    /// <para>
    /// **An address is shown partly hidden even though the member typed it themselves.** A payout
    /// list is read over somebody's shoulder as often as any other screen, and a full email address
    /// beside a balance is more than the row needs to say.
    /// </para>
    /// </summary>
    private static string Mask(string provider, string identity)
    {
        var name = provider.Length == 0
            ? "Payout"
            : char.ToUpperInvariant(provider[0]) + provider[1..];

        var at = identity.IndexOf('@', StringComparison.Ordinal);

        if (at <= 0)
        {
            // A phone number or an opaque id: keep the tail, which is what somebody recognises.
            var tail = identity.Length <= 4 ? identity : identity[^4..];

            return $"{name} ••••{tail}";
        }

        var local = identity[..at];
        var domain = identity[at..];
        var head = local.Length <= 2 ? local : local[..2];

        return $"{name} {head}•••{domain}";
    }

    // ── Refreshing ──────────────────────────────────────────────────────────────────────────

    public async Task<Result<PayoutDestinationResponse>> Handle(
        RefreshPayoutDestinationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<PayoutDestinationResponse>(caller.Error);
        }

        var destination = await _destinations.FindAsync(
            PayoutDestinationId.From(command.DestinationId), cancellationToken);

        // 404 rather than 403 on somebody else's destination, so an identifier cannot be used to
        // discover that one exists. The same rule the transaction read follows.
        if (destination is null || destination.OwnerUserId != caller.Value)
        {
            return Result.Failure<PayoutDestinationResponse>(Error.NotFound(
                FinancialErrorCodes.DestinationNotFound, "That payout destination does not exist."));
        }

        if (destination.IsVerified)
        {
            return Result.Success(FinancialMapperExtensions.ToResponse(destination));
        }

        var source = _verification.FirstOrDefault(candidate =>
            string.Equals(candidate.Provider, destination.Provider, StringComparison.OrdinalIgnoreCase));

        if (source is null)
        {
            // Nothing to ask. The destination stays as it is and staff remain the way through,
            // which is the situation before any provider adapter is registered.
            return Result.Success(FinancialMapperExtensions.ToResponse(destination));
        }

        var standing = await source.GetAsync(caller.Value, cancellationToken);

        // **Only ever upward.** A provider that answers "not yet" — or cannot be asked at all —
        // leaves the destination exactly as it was. Withdrawing a verification on a rate limit or a
        // timeout would strand money mid-payout for a reason nobody could see.
        if (standing.IsPayable)
        {
            destination.Verify(_clock.UtcNow, standing.Provider, standing.Reference);
            _destinations.Update(destination);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(FinancialMapperExtensions.ToResponse(destination));
    }

    // ── Fixtures ────────────────────────────────────────────────────────────────────────────

    private async Task<PayoutDestination> OpenDestinationAsync(
        Guid userId,
        string provider,
        string reference,
        string currency,
        string countryCode,
        CancellationToken cancellationToken)
    {
        var account = await _identities.GetOrCreateAsync(
            userId,
            FinancialAccountType.Personal,
            currency,
            countryCode: countryCode,
            cancellationToken: cancellationToken);

        var destination = PayoutDestination.Register(
            PayoutDestinationId.From(_guids.NewSequentialGuid()),
            userId,
            account.Id,

            // The provider holds the account and this platform never sees what is behind it, which
            // is what ProviderManaged means. Calling it a BankAccount would claim knowledge of a
            // sort code nobody here has.
            PayoutDestinationKind.ProviderManaged,

            // A placeholder until the provider says whose account it is. The label is what a member
            // reads on the withdraw screen, and "Barclays ••••4417" is the provider's to give.
            $"{provider} account",
            currency,
            _clock.UtcNow,
            provider,
            reference,
            countryCode);

        _destinations.Add(destination);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return destination;
    }
}
