using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Extensibility.Receipts;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Operations.Scheduling;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Infrastructure.Hosting;

/// <summary>
/// Charges recurring donations whose next date has come round. COMMUNITY-LIFE-GAP-008.
/// <para>
/// <b>Two workers cannot charge one schedule, and there are two independent reasons why.</b> The
/// outer one is B17's <see cref="IDistributedLock"/>: an instance that cannot take the lease skips
/// the cycle rather than queuing behind the one that did. The inner one is the schedule's own
/// claim — <c>TryClaim</c> moves the due date forward and bumps the revision <em>before</em> any
/// money is asked for, and the revision is a concurrency token, so two instances that both loaded
/// the same row race on <c>SaveChanges</c> and exactly one wins. The lease alone would not be
/// enough: a lease can expire mid-run and hand the resource to a second instance while the first is
/// still working.
/// </para>
/// <para>
/// <b>Forward first, charge second.</b> A worker that charged and then moved the date would charge
/// twice if it died in between. Moving first means a crash at worst skips a cycle — and a missed
/// donation is recoverable while a double charge on somebody's card is not.
/// </para>
/// <para>
/// <b>Each schedule is its own unit of work.</b> One donor's declined card must not roll back the
/// twelve schedules charged before it in the same pass.
/// </para>
/// <para>
/// <b>The charge goes through the ordinary payment path and nothing here marks anything paid.</b>
/// A donation and a payment intent are created exactly as a member's own tap creates them, and
/// <see cref="PaymentProcessor"/> drives the gateway; the capture event completes the donation and
/// moves the cause's totals through the code that already owns that. A worker that set a donation to
/// completed would be a second settlement authority, and the two would disagree the first time a
/// gateway declined after the fact.
/// </para>
/// <para>
/// <b>An unattended charge that needs the payer is a failure.</b> A 3-D Secure challenge is a
/// perfectly good outcome when somebody is holding the phone and is a dead end at three in the
/// morning; the schedule records it as a retryable failure rather than leaving an intent pending for
/// ever.
/// </para>
/// </summary>
public sealed class RecurringDonationSweep : FinancialSweep
{
    private const string LockKey = "financial:recurring-donations";

    public RecurringDonationSweep(
        IServiceScopeFactory scopes,
        IOptions<FinancialOptions> options,
        ILogger<RecurringDonationSweep> logger)
        : base(scopes, options, logger)
    {
    }

    protected override bool IsEnabled =>
        Options.EnableRecurringDonations && Options.EnableRecurringDonationSweep;

    protected override TimeSpan Interval =>
        TimeSpan.FromMinutes(Math.Max(1, Options.RecurringDonationSweepMinutes));

    public override Task<int> RunOnceAsync(CancellationToken cancellationToken = default) =>
        RunOnceAsync(asOf: null, cancellationToken);

    /// <summary>
    /// One pass, as at a given moment.
    /// <para>
    /// <b>The moment is a parameter so a test can say "a month has passed" without a second
    /// clock.</b> Every sweep on this platform is already public for the same family of reason —
    /// asserting on a side effect after a timer happens to have fired is a flaky test wearing a
    /// passing badge — and this goes one step further because the thing under test is a schedule,
    /// and a schedule is only interesting once its date has come round. The alternative, a mutable
    /// clock registered host-wide, would move token expiry and every other deadline with it.
    /// </para>
    /// <para>
    /// Null means now. Nothing in production passes anything else.
    /// </para>
    /// </summary>
    public async Task<int> RunOnceAsync(
        DateTimeOffset? asOf, CancellationToken cancellationToken = default)
    {
        using var scope = Scopes.CreateScope();

        var locks = scope.ServiceProvider.GetService<IDistributedLock>();

        var lease = locks is null
            ? null
            : await locks.TryAcquireAsync(
                LockKey,
                TimeSpan.FromMinutes(Math.Max(6, Options.RecurringDonationSweepMinutes + 1)),
                cancellationToken);

        if (locks is not null && lease is null)
        {
            Logger.LogDebug("Another instance holds the recurring donation lease.");

            return 0;
        }

        try
        {
            return await ChargeAsync(scope.ServiceProvider, asOf, cancellationToken);
        }
        finally
        {
            if (lease is not null)
            {
                await lease.DisposeAsync();
            }
        }
    }

    private async Task<int> ChargeAsync(
        IServiceProvider services, DateTimeOffset? asOf, CancellationToken cancellationToken)
    {
        var schedules = services.GetRequiredService<IRecurringDonationRepository>();
        var clock = services.GetRequiredService<IClock>();
        var now = asOf ?? clock.UtcNow;

        var due = await schedules.ListDueAsync(now, Options.SweepBatchSize, cancellationToken);
        var handled = 0;

        foreach (var schedule in due)
        {
            // A scope each, so one donor's failure is one donor's failure. Sharing the outer scope
            // would share a change tracker, and a rollback would take the whole pass with it.
            using var inner = Scopes.CreateScope();

            if (await ChargeOneAsync(inner.ServiceProvider, schedule.Id, now, cancellationToken))
            {
                handled++;
            }
        }

        return handled;
    }

    private async Task<bool> ChargeOneAsync(
        IServiceProvider services,
        RecurringDonationId scheduleId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var schedules = services.GetRequiredService<IRecurringDonationRepository>();
        var causes = services.GetRequiredService<IDonationCauseRepository>();
        var donations = services.GetRequiredService<IDonationRepository>();
        var intents = services.GetRequiredService<IPaymentIntentRepository>();
        var receipts = services.GetRequiredService<IReceiptReferenceGenerator>();
        var identities = services.GetRequiredService<FinancialIdentityService>();
        var processor = services.GetRequiredService<PaymentProcessor>();
        var guids = services.GetRequiredService<IGuidGenerator>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();

        var schedule = await schedules.FindAsync(scheduleId, cancellationToken);

        if (schedule is null || !schedule.TryClaim(now))
        {
            // Another instance took it between the listing and here. Not an error.
            return false;
        }

        schedules.Update(schedule);

        var cause = await causes.FindByCodeAsync(schedule.CauseCode, cancellationToken);

        if (cause is null || !cause.IsActive)
        {
            // The cause stopped collecting. Ending the schedule is the honest answer — a standing
            // instruction to give to something that no longer exists is not something to retry.
            schedule.CompleteBecauseCauseClosed(now);
            schedules.Update(schedule);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return true;
        }

        try
        {
            var donation = Donation.Pledge(
                DonationId.From(guids.NewSequentialGuid()),
                schedule.DonorUserId,
                isAnonymous: false,
                cause.BeneficiaryType,
                schedule.Amount,
                receipts.Next("DON", now),
                now,
                cause.BeneficiaryId,
                cause.Id,
                cause.Code,
                message: null,
                cause.CountryCode);

            donations.Add(donation);

            var account = await identities.GetOrCreateAsync(
                schedule.DonorUserId,
                FinancialAccountType.Personal,
                schedule.Currency,
                countryCode: cause.CountryCode,
                cancellationToken: cancellationToken);

            var intent = PaymentIntent.Create(
                PaymentIntentId.From(guids.NewSequentialGuid()),
                schedule.DonorUserId,
                account.Id,
                schedule.Amount,
                PaymentMethodKind.Card,
                $"Recurring donation: {cause.Title}",

                now,
                subject: null,

                // Derived from the schedule and the cycle, so a retry of the same cycle finds the
                // intent it already created rather than opening a second one. This is the guard
                // against the sweep charging twice for one period.
                idempotencyKey: $"recurring:{schedule.Id.Value:N}:{schedule.ChargeCount + 1}",
                countryCode: cause.CountryCode);

            // Scoped to the donor. The key already namespaces the schedule and the cycle, so
            // this changes nothing for the sweep — but an unscoped lookup is the shape that
            // invites the defect back, and the sweep is the one caller that could have been
            // left alone without anybody noticing.
            var duplicate = await intents.FindByIdempotencyKeyAsync(
                intent.PayerUserId, intent.IdempotencyKey!, cancellationToken);

            if (duplicate is not null)
            {
                Logger.LogWarning(
                    "Recurring donation {Schedule} cycle {Cycle} was already attempted. Skipping.",
                    schedule.Id.Value,
                    schedule.ChargeCount + 1);

                await unitOfWork.SaveChangesAsync(cancellationToken);

                return false;
            }

            intents.Add(intent);
            donation.AttachPayment(intent.Id);

            var authorized = await processor.AuthorizeAsync(
                intent, clientContext: null, cancellationToken);

            // A provider with a separate authorisation step leaves the intent Authorized, and money
            // reserved is not money taken. Nobody is holding the phone to press "confirm", so the
            // capture happens here — the same second call a member's own checkout makes, and the
            // same one that raises the capture event the donation completes on.
            if (authorized.IsSuccess && intent.Status == PaymentIntentStatus.Authorized)
            {
                await processor.CaptureAsync(intent, amount: null, cancellationToken);
            }

            if (authorized.IsFailure)
            {
                schedule.RecordFailure(
                    authorized.Error.Description, Options.RecurringDonationRetryDays, now);
                donation.Fail(authorized.Error.Description, now);
            }
            else if (intent.Status is PaymentIntentStatus.Captured or PaymentIntentStatus.Settled)
            {
                // The capture event has already completed the donation and moved the cause's
                // totals. All that is left is to say the cycle worked.
                schedule.RecordCharge(donation.Id, now);
            }
            else
            {
                var reason = intent.Status == PaymentIntentStatus.Pending
                    ? "The payment needs the donor to confirm it, and nobody was there to."
                    : $"The payment did not complete: {intent.FailureMessage ?? intent.Status.ToString()}.";

                schedule.RecordFailure(reason, Options.RecurringDonationRetryDays, now);

                // Left as it stands rather than forced terminal: an intent that requires action can
                // still be completed by the donor from their wallet, and failing it here would take
                // that away.
                donation.Fail(reason, now);
            }

            donations.Update(donation);
            schedules.Update(schedule);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogError(
                exception,
                "Recurring donation {Schedule} could not be charged. It will be retried.",
                scheduleId.Value);

            // The claim moved the date forward and was not saved, because the save is what threw.
            // The schedule is therefore still due and the next pass tries again — which is the
            // behaviour that makes a transient database failure cost a delay rather than a donation.
            return false;
        }
    }
}
