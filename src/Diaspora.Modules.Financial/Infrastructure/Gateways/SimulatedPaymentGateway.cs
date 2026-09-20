using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Extensibility.Gateways;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Financial.Infrastructure.Gateways;

/// <summary>
/// The gateway that ships: deterministic, offline, and named after nobody.
/// <para>
/// <b>It exists so the whole payment lifecycle is exercisable without a provider account.</b> Every
/// state transition, every webhook path, every refund and every failure branch is reachable in the
/// integration suite because this adapter can be told what to do — and none of it depends on a
/// network, a sandbox key or somebody's test card.
/// </para>
/// <para>
/// <b>Behaviour is driven by the amount, which is the only lever a test has.</b> A minor-unit value
/// ending in 01 declines, 02 is unavailable, 03 asks for a challenge; everything else succeeds. That
/// is crude on purpose: a configuration-driven simulator would need configuration in every test, and
/// the amounts read clearly in an assertion.
/// </para>
/// <para>
/// <b>An architecture test asserts that no type in this assembly names a payment vendor.</b> Stripe,
/// Paystack, Flutterwave, Adyen, Telr and Peach Payments are adapters this module has never heard
/// of; a deployment registers one and this class stops being resolved.
/// </para>
/// </summary>
public sealed class SimulatedPaymentGateway : IPaymentGateway
{
    /// <summary>The adapter's name, stored on the intent and never interpreted.</summary>
    public const string ProviderName = "simulated";

    private const long DeclineSuffix = 1;
    private const long UnavailableSuffix = 2;
    private const long ChallengeSuffix = 3;

    private readonly ILogger<SimulatedPaymentGateway> _logger;

    public SimulatedPaymentGateway(ILogger<SimulatedPaymentGateway> logger) => _logger = logger;

    public string Provider => ProviderName;

    public IReadOnlySet<PaymentMethodKind> SupportedMethods { get; } =
        new HashSet<PaymentMethodKind>
        {
            PaymentMethodKind.Card,
            PaymentMethodKind.BankTransfer,
            PaymentMethodKind.MobileMoney,
            PaymentMethodKind.ManualTransfer
        };

    /// <summary>Empty: the simulator does not restrict currency, so a deployment can test any.</summary>
    public IReadOnlySet<string> SupportedCurrencies { get; } = new HashSet<string>(StringComparer.Ordinal);

    public bool SupportsSeparateAuthorization => true;

    public Task<PaymentGatewayResult> AuthorizeAsync(
        PaymentGatewayRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Simulate(request, "auth"));

    public Task<PaymentGatewayResult> CaptureAsync(
        PaymentGatewayRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Simulate(request, "cap"));

    /// <summary>
    /// Every intent this simulator has been asked to void, newest first.
    /// <para>
    /// <b>Here so that releasing a hold can be proved rather than assumed.</b> Voiding an
    /// authorisation leaves no trace on the intent — it is already <c>Failed</c> — so a test with
    /// only the aggregate to look at passes whether or not the provider was ever told to let the
    /// member's money go. That is exactly how <c>CancelAsync</c> came to have no caller anywhere
    /// in the module while every adapter implemented it.
    /// </para>
    /// </summary>
    public static IReadOnlyCollection<Guid> Voided => VoidedIntents;

    private static readonly System.Collections.Concurrent.ConcurrentBag<Guid> VoidedIntents = [];

    public Task<PaymentGatewayResult> CancelAsync(
        PaymentGatewayRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        VoidedIntents.Add(request.PaymentIntentId);

        return Task.FromResult(PaymentGatewayResult.Success(
            Reference(request, "cxl"), "cancelled"));
    }

    /// <summary>
    /// Refunds always succeed here.
    /// <para>
    /// Deliberate: a refund that a simulator could refuse would make the refund tests assert on the
    /// simulator rather than on the platform, and the interesting refusals — larger than captured,
    /// wrong currency, not captured yet — are all the aggregate's and all already covered.
    /// </para>
    /// </summary>
    public Task<PaymentGatewayResult> RefundAsync(
        PaymentGatewayRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(PaymentGatewayResult.Success(
            Reference(request, "ref"), "refunded"));
    }

    private PaymentGatewayResult Simulate(PaymentGatewayRequest request, string operation)
    {
        ArgumentNullException.ThrowIfNull(request);

        var suffix = request.Amount.MinorUnits % 100;

        switch (suffix)
        {
            case DeclineSuffix:
                _logger.LogInformation(
                    "Simulated decline for intent {PaymentIntentId}.", request.PaymentIntentId);

                return PaymentGatewayResult.Declined(
                    "card_declined", "The issuer declined this payment.");

            case UnavailableSuffix:
                return PaymentGatewayResult.Unavailable("The simulated gateway is unreachable.");

            case ChallengeSuffix:
                return PaymentGatewayResult.Challenge(
                    $"simulated-challenge:{request.PaymentIntentId:N}", Reference(request, operation));

            default:
                return PaymentGatewayResult.Success(Reference(request, operation), "succeeded");
        }
    }

    private static string Reference(PaymentGatewayRequest request, string operation) =>
        $"sim_{operation}_{request.PaymentIntentId:N}";
}
