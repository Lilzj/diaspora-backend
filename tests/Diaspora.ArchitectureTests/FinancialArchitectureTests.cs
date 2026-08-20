using System.Reflection;
using System.Runtime.CompilerServices;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.Modules.Financial.Extensibility.Currency;
using Diaspora.Modules.Financial.Extensibility.Gateways;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Platform.Modules.Contributors;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The boundaries the Financial Platform must not cross, enforced rather than documented.
/// <para>
/// Each of these is true on the day it is written and quietly false two releases later unless
/// something checks. Three of them are the phase's central claims — the ledger is immutable, the
/// wallet is a projection, and no payment vendor appears in this assembly — and every one of those
/// is the kind of rule that would otherwise be a sentence in a report nobody re-reads.
/// </para>
/// </summary>
public class FinancialArchitectureTests
{
    private static readonly Assembly Module = typeof(FinancialModule).Assembly;

    /// <summary>
    /// <b>The Financial Platform owns no business data, and this is how that is checked.</b> It
    /// references no vertical at all — not Marketplace, not Jobs, not Housing, not Trust, not even
    /// Identity. A placement over a marketplace listing knows a type and a GUID; if that is not
    /// enough, the fix is to enrich a published contract, never to reach into the module.
    /// </summary>
    [Fact]
    public void The_module_references_no_other_module()
    {
        var result = Types.InAssembly(Module)
            .Should()
            .NotHaveDependencyOnAny(
                "Diaspora.Modules.Trust",
                "Diaspora.Modules.Location",
                "Diaspora.Modules.Media",
                "Diaspora.Modules.Community",
                "Diaspora.Modules.Providers",
                "Diaspora.Modules.Guides",
                "Diaspora.Modules.Jobs",
                "Diaspora.Modules.Marketplace",
                "Diaspora.Modules.Housing",
                "Diaspora.Modules.CommunityLife",
                "Diaspora.Modules.Notifications",
                "Diaspora.Modules.Recommendations",
                "Diaspora.Modules.Bookmarks",
                "Diaspora.Domain.Identity")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the Financial Platform consumes published contracts and nothing else. Offenders: "
            + Describe(result));
    }

    /// <summary>
    /// <b>Only integration events cross a module boundary.</b> Every consumer derives from
    /// <c>IntegrationEventHandler</c>, so a consumer written against the domain pipeline is a compile
    /// error rather than a silent bypass of the outbox.
    /// </summary>
    [Fact]
    public void Every_cross_module_consumer_is_an_integration_event_handler()
    {
        var offenders = Module.GetTypes()
            .Where(type => type.Namespace?.Contains(".Application.Consumers", StringComparison.Ordinal) == true)
            .Where(type => type is { IsAbstract: false, IsInterface: false })

            // Iterator and async methods compile to nested state-machine types in the same
            // namespace. They are not consumers, and a rule that counted them would fail on every
            // consumer that awaits — which is all of them.
            .Where(type => !IsCompilerGenerated(type))
            .Where(type => !IsIntegrationEventConsumer(type))
            .ToList();

        offenders.Should().BeEmpty(
            "a consumer of another module's fact must ride the integration pipeline. Offenders: "
            + string.Join(", ", offenders.Select(type => type.FullName)));
    }

    /// <summary>
    /// <b>The ledger is immutable, and the type is what enforces it.</b> One factory, no public
    /// setter, no public mutator, no soft delete and no auditable interface — because both of those
    /// exist to record that something changed and nothing about an entry ever does. Correcting the
    /// ledger means appending a compensating entry.
    /// </summary>
    [Fact]
    public void A_ledger_entry_cannot_be_changed_after_it_is_appended()
    {
        typeof(LedgerEntry)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is { IsPublic: true })
            .Should().BeEmpty("the ledger is append-only");

        typeof(LedgerEntry)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Should().BeEmpty("there is no operation on an entry but reading it");

        typeof(ISoftDeletable).IsAssignableFrom(typeof(LedgerEntry)).Should().BeFalse();
        typeof(IAuditableEntity).IsAssignableFrom(typeof(LedgerEntry)).Should().BeFalse();

        typeof(LedgerEntry)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.ReturnType == typeof(LedgerEntry))
            .Should().ContainSingle("there is exactly one way an entry comes into existence");
    }

    /// <summary>
    /// <b>The wallet is a projection and never a source of truth.</b> No public member sets a
    /// balance; the only one that moves one takes a <see cref="LedgerEntry"/>. This is the whole
    /// architectural claim of the phase, expressed as an assertion rather than a sentence in a
    /// report.
    /// </summary>
    [Fact]
    public void A_wallet_has_no_way_to_set_a_balance()
    {
        string[] forbidden =
        [
            "Credit", "Debit", "Deposit", "Withdraw", "SetBalance", "AdjustBalance",
            "AddFunds", "RemoveFunds", "SetAvailable", "SetPending", "SetReserved", "Transfer"
        ];

        var offenders = typeof(Wallet)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => forbidden.Contains(method.Name, StringComparer.Ordinal))
            .Select(method => method.Name)
            .ToList();

        offenders.Should().BeEmpty(
            "balances move because entries are appended, and for no other reason. Offenders: "
            + string.Join(", ", offenders));

        typeof(Wallet)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.Name.EndsWith("MinorUnits", StringComparison.Ordinal))
            .Where(property => property.SetMethod is { IsPublic: true })
            .Should().BeEmpty("a balance is never assigned from outside the aggregate");
    }

    /// <summary>
    /// <b>There is exactly one door into the ledger.</b> A second implementation would be a path
    /// where the balancing rule, the wallet status check, the sequence assignment and the
    /// balance-before check do not apply — and the enforcement is only as good as the number of ways
    /// around it.
    /// </summary>
    [Fact]
    public void Only_one_type_posts_to_the_ledger()
    {
        var implementations = Module.GetTypes()
            .Where(type => typeof(ILedgerPostingService).IsAssignableFrom(type))
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .ToList();

        implementations.Should().ContainSingle(
            "one door into the ledger is what makes the balancing rule unavoidable");
    }

    /// <summary>
    /// <b>No payment vendor is named anywhere in this assembly.</b> Stripe, Paystack, Flutterwave,
    /// Adyen, Telr and Peach Payments are adapters this module has never heard of; the check is
    /// deliberately crude — a substring match — because the failure it guards against is somebody
    /// adding <c>StripeGateway</c> here rather than to an adapter package.
    /// </summary>
    [Fact]
    public void No_payment_vendor_is_named_in_this_assembly()
    {
        string[] vendors =
        [
            "Stripe", "Paystack", "Flutterwave", "Adyen", "Telr", "PeachPayments", "Peach",
            "Braintree", "PayPal", "Checkout", "Razorpay", "Mpesa", "MPesa", "Wise", "Revolut",
            "Square", "Klarna", "GoCardless", "Mollie", "Worldpay"
        ];

        var offenders = Module.GetTypes()
            .Where(type => !IsCompilerGenerated(type))
            .Where(type => vendors.Any(vendor =>
                type.Name.Contains(vendor, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        offenders.Should().BeEmpty(
            "the shipped gateway is a simulator and every real provider is an adapter in its own "
            + "assembly. Offenders: " + string.Join(", ", offenders.Select(type => type.FullName)));
    }

    /// <summary>
    /// <b>No AI vendor either.</b> The platform-wide rule, checked here because this module declares
    /// four AI-readiness seams and is exactly where a model would first be tempted in.
    /// </summary>
    [Fact]
    public void No_ai_vendor_is_named_in_this_assembly()
    {
        string[] vendors =
        [
            "OpenAi", "Anthropic", "Claude", "Gpt", "Llama", "Gemini", "Cohere", "HuggingFace",
            "TensorFlow", "PyTorch", "Onnx", "Sagemaker", "Vertex"
        ];

        var offenders = Module.GetTypes()
            .Where(type => !IsCompilerGenerated(type))
            .Where(type => vendors.Any(vendor =>
                type.Name.Contains(vendor, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        offenders.Should().BeEmpty(
            "fraud detection, smart pricing, forecasting and churn prediction are seams; an "
            + "implementation belongs in its own assembly. Offenders: "
            + string.Join(", ", offenders.Select(type => type.FullName)));
    }

    /// <summary>
    /// <b>Conversion and settlement are seams with nothing behind them, and that is deliberate.</b>
    /// The counterpart to the vendor rules: those would pass on a platform where the extension points
    /// had been deleted. If either of these ever gains an implementation here, the module has
    /// quietly acquired a capability its documentation says it does not have.
    /// </summary>
    [Fact]
    public void Conversion_and_settlement_remain_declared_and_unimplemented()
    {
        Module.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(typeof(IExchangeRateProvider).IsAssignableFrom)
            .Should().BeEmpty(
                "the platform never converts; a rate has a date and a stored figure restated at "
                + "yesterday's rate is a number somebody would act on and be wrong about");

        Module.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(typeof(ISettlementProvider).IsAssignableFrom)
            .Should().BeEmpty("no settlement engine ships in this phase; the seam is the deliverable");

        typeof(IExchangeRateProvider).Should().NotBeNull();
        typeof(ISettlementProvider).Should().NotBeNull();
    }

    /// <summary>
    /// <b>The gateway abstraction is real: at least one implementation exists and it is not a
    /// vendor.</b> The companion to the vendor rule — which would pass on a platform with no payment
    /// abstraction at all.
    /// </summary>
    [Fact]
    public void A_gateway_abstraction_ships_with_a_working_default()
    {
        Module.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(typeof(IPaymentGateway).IsAssignableFrom)
            .Should().NotBeEmpty("the payment path needs something behind it to be testable");

        Module.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(typeof(IPaymentWebhookProcessor).IsAssignableFrom)
            .Should().NotBeEmpty("the webhook refusal path must be exercisable");
    }

    /// <summary>
    /// <b>The module's URL space is declared in one place.</b> A second prefix appearing elsewhere
    /// would be a financial surface nobody knew existed.
    /// </summary>
    [Fact]
    public void The_module_maps_one_route_prefix()
    {
        var contributors = Module.GetTypes()
            .Where(type => typeof(IEndpointContributor).IsAssignableFrom(type))
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .ToList();

        contributors.Should().ContainSingle("one module, one URL space");

        FinancialModuleMetadata.RoutePrefix.Should().Be("/api/v1/financial");
    }

    /// <summary>
    /// <b>The domain layer does not reference EF Core.</b> The platform rule, checked here because
    /// the aggregates carry query-shaped concepts — cursors, filters, projections — and are the
    /// likeliest place for a persistence type to leak upward.
    /// </summary>
    [Fact]
    public void The_domain_layer_does_not_reference_entity_framework()
    {
        var result = Types.InAssembly(Module)
            .That()
            .ResideInNamespaceStartingWith("Diaspora.Modules.Financial.Domain")
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the domain describes what is true, not how it is stored. Offenders: " + Describe(result));
    }

    /// <summary>
    /// <b>The extensibility layer does not reference the infrastructure that implements it.</b> A
    /// seam that knew its own default is not a seam — an adapter replacing it would find the
    /// abstraction already coupled to what it replaced.
    /// </summary>
    [Fact]
    public void The_extensibility_layer_does_not_reference_its_implementations()
    {
        var result = Types.InAssembly(Module)
            .That()
            .ResideInNamespaceStartingWith("Diaspora.Modules.Financial.Extensibility")
            .Should()
            .NotHaveDependencyOn("Diaspora.Modules.Financial.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "a seam that knows its own default is not a seam. Offenders: " + Describe(result));
    }

    /// <summary>
    /// <b>Nothing in this module creates a notification.</b> B13's monopoly, restated for the module
    /// with the most obvious temptation to break it — a receipt, a payment failure and a subscription
    /// lapse all feel like things that ought to send an email.
    /// </summary>
    [Fact]
    public void The_module_never_creates_a_notification()
    {
        var result = Types.InAssembly(Module)
            .Should()
            .NotHaveDependencyOn("Diaspora.Platform.Services.Abstractions.INotificationPublisher")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "business modules publish integration events; only the Communication Platform creates "
            + "notifications. Offenders: " + Describe(result));
    }

    /// <summary>
    /// <b>Every published financial contract lives in the contracts assembly.</b> A consumer must be
    /// able to reach one without referencing this module — which is the whole reason B13 and B14 can
    /// consume what B15 publishes while B15 references neither.
    /// </summary>
    [Fact]
    public void Every_published_financial_contract_lives_in_the_contracts_assembly()
    {
        Module.GetTypes()
            .Where(type => typeof(IIntegrationEvent).IsAssignableFrom(type))
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Should().BeEmpty("a published contract belongs in Diaspora.Contracts");

        typeof(IIntegrationEvent).Assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith(
                "Diaspora.Contracts.Financial", StringComparison.Ordinal) == true)
            .Where(type => typeof(IIntegrationEvent).IsAssignableFrom(type))
            .Should().HaveCountGreaterThanOrEqualTo(
                15, "B15 publishes the financial lifecycle, not a subset of it");
    }

    private static bool IsIntegrationEventConsumer(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType
                && current.GetGenericTypeDefinition() == typeof(IntegrationEventHandler<>))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCompilerGenerated(Type type) =>
        type.GetCustomAttribute<CompilerGeneratedAttribute>() is not null ||
        type.Name.Contains('<', StringComparison.Ordinal);

    private static string Describe(TestResult result) =>
        result.FailingTypeNames is null
            ? "none reported"
            : string.Join(", ", result.FailingTypeNames);
}
