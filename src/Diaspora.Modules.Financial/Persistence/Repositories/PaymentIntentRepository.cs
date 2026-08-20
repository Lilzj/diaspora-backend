using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>
/// EF Core repository for payment intents.
/// <para>
/// Every read includes the attempts and the refunds. They are the aggregate — a partially loaded
/// intent that a handler then saves would have EF write an empty attempt collection over a real one.
/// </para>
/// </summary>
public sealed class PaymentIntentRepository : IPaymentIntentRepository
{
    private readonly DiasporaDbContext _dbContext;

    public PaymentIntentRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private IQueryable<PaymentIntent> Intents => _dbContext.Set<PaymentIntent>()
        .Include(intent => intent.Attempts)
        .Include(intent => intent.Refunds);

    public void Add(PaymentIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        _dbContext.Set<PaymentIntent>().Add(intent);
    }

    public void Update(PaymentIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        FinancialAggregateUpdater.MarkDirty(_dbContext, intent);
    }

    public async Task<PaymentIntent?> FindAsync(
        PaymentIntentId intentId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intentId);

        return await Intents.FirstOrDefaultAsync(intent => intent.Id == intentId, cancellationToken);
    }

    public async Task<PaymentIntent?> FindByIdempotencyKeyAsync(
        Guid payerUserId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || payerUserId == Guid.Empty)
        {
            return null;
        }

        var normalized = idempotencyKey.Trim();

        // Scoped to the payer, and that scoping is the whole point of this method's
        // signature. Matching on the key alone made an idempotency key a *global* name:
        // a member whose key collided with another's received the other member's intent
        // — its identifier, its amount, its currency, its capture state — at 200, and
        // their own payment was never created. Neither party saw an error.
        //
        // A collision needs no malice to happen. A client that derives a key from an
        // operation and a subject id produces keys that collide between members by
        // construction, which is the natural way to write one.
        return await Intents.FirstOrDefaultAsync(
            intent => intent.IdempotencyKey == normalized && intent.PayerUserId == payerUserId,
            cancellationToken);
    }

    public async Task<PaymentIntent?> FindByExternalReferenceAsync(
        string provider, string externalReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(externalReference))
        {
            return null;
        }

        var normalizedProvider = provider.Trim();
        var normalizedReference = externalReference.Trim();

        return await Intents.FirstOrDefaultAsync(
            intent => intent.GatewayProvider == normalizedProvider
                && intent.GatewayExternalReference == normalizedReference,
            cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentIntent>> ListByPayerAsync(
        Guid payerUserId,
        PaymentIntentStatus? status,
        int take,
        CancellationToken cancellationToken = default)
    {
        var intents = Intents.Where(intent => intent.PayerUserId == payerUserId);

        if (status is { } wanted)
        {
            intents = intents.Where(intent => intent.Status == wanted);
        }

        return await intents
            .OrderByDescending(intent => intent.CreatedAt)
            .Take(Math.Clamp(take, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentIntent>> ListByPayerAsync(
        Guid payerUserId,
        PaymentIntentStatus? status,
        PaymentPurpose? purpose,
        DateTimeOffset? before,
        IReadOnlyList<Guid> excludeIds,
        int take,
        CancellationToken cancellationToken = default)
    {
        var intents = Intents.Where(intent => intent.PayerUserId == payerUserId);

        if (status is { } wantedStatus)
        {
            intents = intents.Where(intent => intent.Status == wantedStatus);
        }

        if (purpose is { } wantedPurpose)
        {
            intents = intents.Where(intent => intent.Purpose == wantedPurpose);
        }

        if (before is { } boundary)
        {
            intents = intents.Where(intent => intent.CreatedAt <= boundary);
        }

        // Excluded by identity rather than by a strict timestamp comparison, so rows sharing the
        // boundary instant are served once and never dropped. ADR-017.
        if (excludeIds is { Count: > 0 })
        {
            var excluded = excludeIds.Select(PaymentIntentId.From).ToList();

            intents = intents.Where(intent => !excluded.Contains(intent.Id));
        }

        return await intents
            .OrderByDescending(intent => intent.CreatedAt)
            .Take(Math.Clamp(take, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentIntent>> ListUnsettledAsync(
        DateTimeOffset capturedBefore, int take, CancellationToken cancellationToken = default) =>
        await Intents
            .Where(intent => intent.Status == PaymentIntentStatus.Captured
                && intent.CapturedAt != null
                && intent.CapturedAt <= capturedBefore)
            .OrderBy(intent => intent.CapturedAt)
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PaymentIntent>> ListExpiredAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default) =>
        await Intents
            .Where(intent =>
                (intent.Status == PaymentIntentStatus.Draft
                    || intent.Status == PaymentIntentStatus.Pending)
                && intent.ExpiresAt <= asOf)
            .OrderBy(intent => intent.ExpiresAt)
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken);
}
