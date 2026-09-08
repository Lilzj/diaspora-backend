using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Payments.PayPal;
using Diaspora.Platform.Services.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Payments;

/// <summary>
/// The PayPal adapter against a real sandbox account.
///
/// <para>
/// <b>This suite is named in the adapter's own summary and did not exist.</b>
/// <c>PayPalSettlementProvider</c> says its payload "is written from PayPal's REST payouts API and
/// has not been executed against a live account", and that "until somebody runs
/// <c>LivePayPalSandboxTests</c> with real sandbox credentials, every capability row for this
/// provider should stay <c>Unknown</c> so nothing routes to it". The rule was written; the thing it
/// pointed at never was. Every PayPal row has therefore been correctly stuck on <c>Unknown</c> — and
/// with it Mauritius, which Stripe has no corridor for at all.
/// </para>
///
/// <para>
/// <b>Two tiers, because one of these moves money and the other does not.</b> The token tests need
/// only credentials. The payout test additionally needs
/// <c>PAYPAL_SANDBOX_RECEIVER</c> — a deliberate third variable, so a suite run cannot send a payout
/// as a side effect of somebody having credentials in their shell.
/// </para>
///
/// <code>
/// PAYPAL_CLIENT_ID=... PAYPAL_CLIENT_SECRET=... \
///   dotnet test tests/Diaspora.UnitTests --filter FullyQualifiedName~LivePayPalSandboxTests
///
/// # and, to prove the payout payload itself:
/// PAYPAL_SANDBOX_RECEIVER=sb-buyer@personal.example.com ...
/// </code>
/// </summary>
public sealed class LivePayPalSandboxTests
{
    private const string ClientIdVariable = "PAYPAL_CLIENT_ID";
    private const string ClientSecretVariable = "PAYPAL_CLIENT_SECRET";
    private const string ReceiverVariable = "PAYPAL_SANDBOX_RECEIVER";

    // ── The guard ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A guard rather than an assertion about PayPal: live credentials here would mean somebody
    /// pointed the suite at production, where the payout test sends real money to a real person.
    /// </summary>
    [LivePayPal]
    public void The_live_suite_refuses_to_run_against_production()
    {
        Settings().IsSandbox.Should().BeTrue(
            $"{ClientIdVariable} must be a sandbox app; point this suite at a sandbox account");
    }

    // ── The credential ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The credentials authenticate. A null here is a wrong or rolled client id or secret, and it is
    /// the first thing to check when a payout refuses with <c>paypal.unauthorized</c>.
    /// </summary>
    [LivePayPal]
    public async Task The_credentials_mint_an_access_token()
    {
        var token = await Tokens().GetAsync(DateTimeOffset.UtcNow);

        token.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// The second call does not go back to PayPal.
    /// <para>
    /// The cache is a singleton precisely so a token is minted once and reused, and PayPal rate
    /// limits the token endpoint far more tightly than the payouts one. A cache that missed every
    /// time would work in testing and throttle under a sweep.
    /// </para>
    /// </summary>
    [LivePayPal]
    public async Task The_token_is_reused_rather_than_minted_again()
    {
        var tokens = Tokens();
        var now = DateTimeOffset.UtcNow;

        var first = await tokens.GetAsync(now);
        var second = await tokens.GetAsync(now.AddSeconds(1));

        second.Should().Be(first);
    }

    // ── The payload ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A one-item payout batch is accepted, which is the only way to know the payload is right.
    /// <para>
    /// <b>This is what the adapter's caveat was waiting for.</b> Every other PayPal test in this
    /// repository asserts the request this adapter *builds*; none of them proves PayPal accepts it.
    /// A field named wrongly, a currency in the wrong case, a missing <c>sender_batch_header</c> —
    /// all of it passes a stubbed test and fails the first real payout.
    /// </para>
    /// <para>
    /// GBP because that is the corridor this unblocks: the Mauritian rupee is not one of PayPal's
    /// payout currencies, so a Mauritian member is paid in GBP or not at all.
    /// </para>
    /// </summary>
    [LivePayPalPayout]
    public async Task A_payout_batch_is_accepted_by_paypal()
    {
        var outcome = await Provider().SubmitAsync(Instruction("accept"));

        outcome.Disposition.Should().Be(
            SettlementDisposition.Accepted,
            "PayPal refused the payload this adapter builds: {0} {1}",
            outcome.Code,
            outcome.Message);

        // The batch id, which reconciliation later matches a withdrawal against. Without it a payout
        // is accepted and unfindable.
        outcome.ProviderReference.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// The same instruction submitted twice is not paid twice.
    /// <para>
    /// <c>sender_batch_id</c> is derived from the withdrawal, and PayPal rejects a batch whose sender
    /// id it has seen. That rejection is what makes a resubmission after a lost response safe, and
    /// it is a property of PayPal's service rather than of this code — which is exactly why it can
    /// only be proved here.
    /// </para>
    /// </summary>
    [LivePayPalPayout]
    public async Task The_same_instruction_twice_does_not_pay_twice()
    {
        var provider = Provider();
        var instruction = Instruction("duplicate");

        var first = await provider.SubmitAsync(instruction);
        var second = await provider.SubmitAsync(instruction);

        first.Disposition.Should().Be(
            SettlementDisposition.Accepted,
            "PayPal refused the payload: {0} {1}",
            first.Code,
            first.Message);

        // **A duplicate must come back Accepted, and Refused here is a double-payment bug.**
        //
        // The adapter says so itself: "a batch PayPal has already seen is one it already acted on.
        // That is a success being reported as a conflict, and treating it as a refusal would release
        // a hold on money that has gone." The sweep acts on exactly that distinction — an unknown
        // outcome holds the balance, a refusal releases it — so a misread duplicate hands the money
        // back to somebody PayPal has already paid.
        //
        // The first live run refused it: PayPal answered USER_BUSINESS_ERROR and `IsDuplicateBatch`
        // looks for BATCH_ID_ALREADY_EXISTS. Whether that marker is simply wrong is what the
        // detail-carrying error message now answers.
        second.Disposition.Should().Be(
            SettlementDisposition.Accepted,
            "a resubmitted batch must not release the hold: {0} {1}",
            second.Code,
            second.Message);

        second.ProviderReference.Should().Be(
            first.ProviderReference,
            "the second submission must name the batch the first created, not a new one");
    }

    // ── Fixtures ────────────────────────────────────────────────────────────────────────────

    private static PayPalOptions Settings() => new()
    {
        ClientId = Environment.GetEnvironmentVariable(ClientIdVariable) ?? string.Empty,
        ClientSecret = Environment.GetEnvironmentVariable(ClientSecretVariable) ?? string.Empty,

        // Sandbox by default and never overridden from the environment here. Pointing this suite at
        // production is not a configuration mistake somebody should be able to make.
        ApiBase = PayPalOptions.SandboxApiBase,
    };

    private static PayPalAccessTokenCache Tokens() => new(
        new StaticOptions(Settings()),
        new HttpClient(),
        NullLogger<PayPalAccessTokenCache>.Instance);

    private static PayPalSettlementProvider Provider() => new(
        new StaticOptions(Settings()),
        Tokens(),
        new HttpClient(),
        new SystemClock(),
        NullLogger<PayPalSettlementProvider>.Instance);

    /// <summary>
    /// One small payout to the sandbox receiver.
    /// <para>
    /// The reference is fixed per run rather than random, because the idempotency test resubmits it
    /// deliberately. A new one each time would make that test pass by never colliding.
    /// </para>
    /// </summary>
    private static SettlementInstruction Instruction(string key)
    {
        var now = DateTimeOffset.UtcNow;

        return new SettlementInstruction
        {
            CounterpartyId = Guid.Parse("b84be125-8f73-427f-acd7-c7f495bd025d"),
            Amount = Money.FromMinorUnits(100, "GBP", 2),
            PeriodStart = now.AddDays(-1),
            PeriodEnd = now,

            // **A key per test, and the first run of this suite proved why.**
            //
            // It was `diaspora-live-{yyyyMMddHHmm}` — minute resolution, shared by every test in
            // the run. Both payout tests submitted the same `sender_batch_id`, PayPal refused the
            // second as a duplicate, and the failure read as "PayPal refused the payload this
            // adapter builds" when the payload was fine. A test fixture accused the code under test.
            //
            // PayPal remembers a sender_batch_id for **thirty days**, so the key carries the tick
            // count as well: re-running this suite twice in one minute must not resurrect the same
            // false failure.
            Reference = $"diaspora-live-{key}-{now:yyyyMMddHHmmss}-{now.Ticks % 100_000}",
            DestinationReference = Environment.GetEnvironmentVariable(ReceiverVariable),
            DestinationProvider = "paypal",
            DestinationKind = PayoutDestinationKind.ProviderManaged,
            DestinationCountry = "MU",
            DestinationLabel = "PayPal sandbox receiver",
        };
    }

    private sealed class StaticOptions(PayPalOptions value) : IOptionsMonitor<PayPalOptions>
    {
        public PayPalOptions CurrentValue => value;

        public PayPalOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<PayPalOptions, string?> listener) => null;
    }

    private sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        public DateTimeOffset Now(string timeZoneId) =>
            TimeZoneInfo.ConvertTime(UtcNow, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
    }
}

/// <summary>Runs only when PayPal sandbox credentials are in the environment.</summary>
public sealed class LivePayPalAttribute : FactAttribute
{
    public LivePayPalAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYPAL_CLIENT_ID"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYPAL_CLIENT_SECRET")))
        {
            Skip = "Set PAYPAL_CLIENT_ID and PAYPAL_CLIENT_SECRET to run this against a PayPal "
                + "sandbox account.";
        }
    }
}

/// <summary>
/// Runs only when a sandbox receiver is named as well.
/// <para>
/// A third variable on purpose: these send a payout, and having credentials in a shell must not be
/// enough to make a suite run move money.
/// </para>
/// </summary>
public sealed class LivePayPalPayoutAttribute : FactAttribute
{
    public LivePayPalPayoutAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYPAL_CLIENT_ID"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYPAL_CLIENT_SECRET")))
        {
            Skip = "Set PAYPAL_CLIENT_ID and PAYPAL_CLIENT_SECRET to run this against a PayPal "
                + "sandbox account.";

            return;
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYPAL_SANDBOX_RECEIVER")))
        {
            Skip = "Set PAYPAL_SANDBOX_RECEIVER to a sandbox personal account's email to let this "
                + "send a payout. It moves sandbox money.";
        }
    }
}
