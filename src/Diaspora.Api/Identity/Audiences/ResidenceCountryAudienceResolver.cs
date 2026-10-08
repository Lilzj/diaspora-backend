using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Users;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.Modules.Notifications.Extensibility.Audiences;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Api.Identity.Audiences;

/// <summary>
/// "Everyone in a country" — the members whose country of residence it is.
/// <para>
/// <b>Why it exists (8 Oct 2026).</b> Notifications declared this question and shipped an answer of
/// "nobody", waiting for an Identity-backed adapter. So the broadcast rules — a featured guide, an
/// updated learning path, and now an official notice — were seeded, tested and delivered to no one.
/// A notice a platform admin published for Mauritius reached no member in Mauritius.
/// </para>
/// <para>
/// <b>Why it lives in the API project.</b> Same reason as the trust-decision bridge: Notifications
/// may not know Identity's types, and the composition root is the one place entitled to know both.
/// Notifications still sees only <see cref="INotificationAudienceResolver"/>.
/// </para>
/// <para>
/// <b>Who counts.</b> Onboarded members only — limited access, waiting on verification, or verified.
/// A half-finished registration has no usable account to read a notice in, and a suspended, locked
/// or disabled one should not be messaged by a broadcast. Oldest members first, so a capped
/// audience is stable from one call to the next rather than an arbitrary slice.
/// </para>
/// </summary>
public sealed class ResidenceCountryAudienceResolver : INotificationAudienceResolver
{
    private readonly DiasporaDbContext _dbContext;

    public ResidenceCountryAudienceResolver(DiasporaDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<UserId>> ResolveCountryAudienceAsync(
        string countryCode,
        string categoryKey,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0 || !CountryCode.TryCreate(countryCode, out var country) || country is null)
        {
            return [];
        }

        var limited = AccessState.LimitedAccess;
        var pending = AccessState.VerificationPending;
        var underReview = AccessState.VerificationUnderReview;
        var verified = AccessState.Verified;

        return await _dbContext.Set<User>()
            .AsNoTracking()
            .Where(user => user.CountryOfResidence == country)
            .Where(user => user.AccessState == limited
                || user.AccessState == pending
                || user.AccessState == underReview
                || user.AccessState == verified)
            .OrderBy(user => user.CreatedAt)
            .Take(limit)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
    }
}
