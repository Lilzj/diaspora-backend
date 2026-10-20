using Diaspora.Contracts.Content.V1;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Users;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Api.Identity.Audiences;

/// <summary>
/// How many members the platform has, and when the recent ones joined — the <c>members</c> row of
/// the platform overview (FRONTEND-PHASE15-GAP-001: "a registered-user count, since no route
/// exposes one").
/// <para>
/// <b>Who counts</b> is the broadcast audience's definition, deliberately: onboarded members only —
/// limited access, waiting on verification, under review, or verified. A half-finished
/// registration is not a member, and a suspended, locked, disabled or erased account is not one any
/// more. A member who joined inside the window and has since left is not counted as joining.
/// </para>
/// <para>
/// Lives in the composition root for the audience resolver's reason: Analytics may not know
/// Identity's types, and only sees <see cref="IPlatformInventorySource"/>.
/// </para>
/// </summary>
public sealed class MemberInventorySource : IPlatformInventorySource
{
    private readonly DiasporaDbContext _dbContext;

    public MemberInventorySource(DiasporaDbContext dbContext) => _dbContext = dbContext;

    public string Key => "members";

    public async Task<PlatformInventoryCount> CountAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var limited = AccessState.LimitedAccess;
        var pending = AccessState.VerificationPending;
        var underReview = AccessState.VerificationUnderReview;
        var verified = AccessState.Verified;

        var members = _dbContext.Set<User>()
            .AsNoTracking()
            .Where(user => user.AccessState == limited
                || user.AccessState == pending
                || user.AccessState == underReview
                || user.AccessState == verified);

        var total = await members.LongCountAsync(cancellationToken);

        var joined = await members
            .Where(user => user.CreatedAt >= from && user.CreatedAt < to)
            .Select(user => user.CreatedAt)
            .ToListAsync(cancellationToken);

        return new PlatformInventoryCount(total, joined);
    }
}
