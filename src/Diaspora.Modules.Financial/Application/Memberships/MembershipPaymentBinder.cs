using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Memberships;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Extensibility.Memberships;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Financial.Application.Memberships;

/// <summary>
/// Decides whether a settled payment bought a community membership, and records it.
/// COMMUNITY-LIFE-GAP-003.
/// <para>
/// <b>A service rather than a command handler, and the reason is a platform rule with teeth.</b>
/// <c>DSP1001</c> refuses a handler that takes <c>ISender</c>, because a handler that can reach any
/// other handler in the process is a dependency graph nobody can read. The route belongs to the
/// module that owns the membership and the URL prefix; the decision belongs here. So the shared
/// behaviour is extracted into this service and injected — which is exactly what the analyzer's own
/// message asks for, and the same shape B18 already uses to make a Community Life write.
/// </para>
/// <para>
/// <b>Not a seam, deliberately.</b> The consuming module already references this one — for the
/// currency catalogue, since B18 shipped — so an interface here would be a level of indirection with
/// exactly one implementation and no second caller in prospect. B15.1's own audit found what that
/// costs: a seam complete, registered, architecture-tested and unreachable. A concrete service that
/// something calls is worth more than an abstraction nothing does.
/// </para>
/// <para>
/// <b>Eight checks, in this order, and each refuses on its own.</b> The caller is signed in and
/// cleared to move money; the intent exists; the caller is the intent's payer; the intent's subject
/// is this membership; the membership exists; the caller holds it; the intent has actually settled;
/// the membership is waiting to be paid for. The ordering matters — asking about a membership before
/// checking who owns the payment would let a caller probe which membership identifiers exist.
/// </para>
/// <para>
/// <b>Idempotency is inherited rather than reinvented.</b> The register says so and it is right: the
/// payment intent is already uniquely keyed by the caller's idempotency key at creation, so a
/// retried checkout arrives naming the same intent, and the unique index on the binding's intent
/// makes the second attempt a replay of the first rather than a second binding.
/// </para>
/// </summary>
public sealed class MembershipPaymentBinder
{
    private readonly IMembershipPaymentBindingRepository _bindings;
    private readonly IPaymentIntentRepository _intents;
    private readonly IMembershipSubjectAuthority _memberships;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MembershipPaymentBinder> _logger;

    public MembershipPaymentBinder(
        IMembershipPaymentBindingRepository bindings,
        IPaymentIntentRepository intents,
        IMembershipSubjectAuthority memberships,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork,
        ILogger<MembershipPaymentBinder> logger)
    {
        _bindings = bindings;
        _intents = intents;
        _memberships = memberships;
        _authorization = authorization;
        _currencies = currencies;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<MembershipPaymentBindingResponse>> BindAsync(
        Guid membershipId, Guid paymentIntentId, CancellationToken cancellationToken = default)
    {
        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<MembershipPaymentBindingResponse>(caller.Error);
        }

        // The financial bar, asked here rather than inherited from whatever gated the route. Every
        // permission that moves money on this platform sits at AccessTier.Verified, and a route in
        // another module's vocabulary must not be able to lower it.
        if (!_authorization.Has(FinancialPermissions.PaymentsManage))
        {
            return Result.Failure<MembershipPaymentBindingResponse>(Error.Forbidden(
                FinancialErrorCodes.Forbidden,
                "Verify your account before paying for a membership."));
        }

        var intentKey = PaymentIntentId.From(paymentIntentId);
        var intent = await _intents.FindAsync(intentKey, cancellationToken);

        // Absent and somebody else's are answered identically. A caller who can tell the two apart
        // can enumerate which payment identifiers exist on this platform.
        if (intent is null || intent.PayerUserId != caller.Value)
        {
            return Result.Failure<MembershipPaymentBindingResponse>(Error.NotFound(
                FinancialErrorCodes.PaymentIntentNotFound,
                "No payment of yours with that identifier."));
        }

        // The subject is what makes an unrelated payment unbindable. The intent says what it was
        // for; a payment for a marketplace boost — or for a different membership — is not this.
        if (intent.Subject is not { } subject
            || !subject.Matches(MonetizationSubjectType.CommunityMembership, membershipId))
        {
            return Result.Failure<MembershipPaymentBindingResponse>(Error.Validation(
                FinancialErrorCodes.MembershipSubjectMismatch,
                "That payment was not made for this membership."));
        }

        var facts = await _memberships.DescribeAsync(membershipId, cancellationToken);

        // Null covers both "no such membership" and "nothing is registered to answer". The second is
        // a deployment that has not composed the owning module, and refusing is the safe direction:
        // binding a payment nobody can vouch for would activate a membership on nobody's authority.
        if (facts is null)
        {
            return Result.Failure<MembershipPaymentBindingResponse>(Error.NotFound(
                FinancialErrorCodes.MembershipNotFound, "No membership with that identifier."));
        }

        if (facts.MemberUserId != caller.Value)
        {
            return Result.Failure<MembershipPaymentBindingResponse>(Error.Forbidden(
                FinancialErrorCodes.Forbidden, "That membership is not yours."));
        }

        var existing = await _bindings.FindByPaymentIntentAsync(intentKey, cancellationToken);

        if (existing is not null)
        {
            // A retry. Answered with the binding that exists and a 200, because the caller asked a
            // question that already has a true answer — and because a 409 here would make an
            // ordinary mobile retry look to the member like a failed payment.
            return Result.Success(await ToResponseAsync(existing, intent, false, cancellationToken));
        }

        // Settlement is the authority. Authorized is money reserved and not money taken, and a
        // capture can still fail after it — which is why it is not on this list.
        if (intent.Status is not (PaymentIntentStatus.Captured or PaymentIntentStatus.Settled))
        {
            return Result.Failure<MembershipPaymentBindingResponse>(Error.Conflict(
                FinancialErrorCodes.MembershipPaymentNotSettled,
                $"That payment is {intent.Status}. A membership activates on settlement, not before."));
        }

        if (!facts.IsAwaitingPayment)
        {
            return Result.Failure<MembershipPaymentBindingResponse>(Error.Conflict(
                FinancialErrorCodes.MembershipPaymentAlreadyBound,
                "That membership is not waiting for a payment."));
        }

        var now = _clock.UtcNow;

        var binding = MembershipPaymentBinding.Record(
            MembershipPaymentBindingId.From(_guids.NewSequentialGuid()),
            membershipId,
            intentKey,
            intent.PayerUserId,
            intent.Captured,
            intent.SettledAt ?? intent.CapturedAt ?? now,
            now,
            facts.OrganizationId);

        _bindings.Add(binding);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (IsUniquenessConflict(exception))
        {
            // Two simultaneous attempts for one payment. The unique index refused this one; the
            // other won, and the answer to both callers is the same binding. Resolved at the
            // database rather than by a handler check, because the window between a check and an
            // insert is exactly where a double-tapped pay button lands.
            _logger.LogInformation(
                "Membership payment {Intent} was bound concurrently. Returning the binding that won.",
                paymentIntentId);

            // The refused row is still tracked as Added. The caller goes on to activate the
            // membership, and that save would retry this insert and hit the same index — so the
            // loser of the race would get a 500 one call after being told it had won.
            _bindings.Forget(binding);

            var winner = await _bindings.FindByPaymentIntentAsync(intentKey, cancellationToken);

            return winner is null
                ? Result.Failure<MembershipPaymentBindingResponse>(Error.Conflict(
                    FinancialErrorCodes.MembershipPaymentAlreadyBound,
                    "That payment could not be bound. Try again."))
                : Result.Success(await ToResponseAsync(winner, intent, false, cancellationToken));
        }

        return Result.Success(await ToResponseAsync(binding, intent, true, cancellationToken));
    }

    /// <summary>
    /// Whether the database refused the write because something was already there.
    /// <para>
    /// Matched on the provider's own text rather than on an exception type, because every provider
    /// raises a different one and this module names no provider. Narrow enough that an ordinary
    /// failure is not mistaken for a race: a wrong answer here would turn a genuine error into a
    /// silent success.
    /// </para>
    /// </summary>
    private static bool IsUniquenessConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;

            if (message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<MembershipPaymentBindingResponse> ToResponseAsync(
        MembershipPaymentBinding binding,
        PaymentIntent intent,
        bool isNew,
        CancellationToken cancellationToken)
    {
        var symbols = await _currencies.AllAsync(cancellationToken);

        return new MembershipPaymentBindingResponse
        {
            BindingId = binding.Id.Value,
            MembershipId = binding.MembershipId,
            PaymentIntentId = binding.PaymentIntentId.Value,
            PaymentStatus = intent.Status.ToString(),
            Amount = FinancialMapper.ToResponse(
                binding.Amount,
                symbols.TryGetValue(binding.Currency, out var facts) ? facts.Symbol : null),
            SettledAt = binding.SettledAt,
            BoundAt = binding.CreatedAt,
            IsNewBinding = isNew
        };
    }
}
