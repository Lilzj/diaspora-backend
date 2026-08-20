using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.Inboxes;
using Diaspora.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Notifications.Persistence.Repositories;

/// <summary>
/// EF Core repository for inbox counters.
/// <para>
/// <b><see cref="GetOrCreateAsync"/> checks the change tracker before the database, and that is not
/// an optimisation.</b> A single unit of work can create several notifications for one member — an
/// event that matches three rules — and each settles the counters. Without the local lookup the
/// second one would query, find nothing (the first is added but not saved), and add a second inbox
/// row that the unique index would reject at save time, failing the whole dispatch.
/// </para>
/// </summary>
public sealed class UserInboxRepository : IUserInboxRepository
{
    private readonly DiasporaDbContext _dbContext;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public UserInboxRepository(DiasporaDbContext dbContext, IGuidGenerator guids, IClock clock)
    {
        _dbContext = dbContext;
        _guids = guids;
        _clock = clock;
    }

    private DbSet<UserInbox> Inboxes => _dbContext.Set<UserInbox>();

    public async Task<UserInbox?> FindAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Inboxes
            .Include(inbox => inbox.Categories)
            .FirstOrDefaultAsync(inbox => inbox.UserId == userId, cancellationToken);
    }

    public async Task<UserInbox> GetOrCreateAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        // The change tracker first. See the class summary — this is correctness, not speed.
        var tracked = _dbContext.ChangeTracker
            .Entries<UserInbox>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(inbox => inbox.UserId == userId);

        if (tracked is not null)
        {
            return tracked;
        }

        if (await FindAsync(userId, cancellationToken) is { } existing)
        {
            return existing;
        }

        var inbox = UserInbox.Open(
            UserInboxId.From(_guids.NewSequentialGuid()), userId, _clock.UtcNow);

        await Inboxes.AddAsync(inbox, cancellationToken);

        return inbox;
    }

    /// <summary>
    /// Members whose counters have not been touched recently.
    /// <para>
    /// Ordered oldest first so the sweep works through the platform rather than repeatedly
    /// re-checking whoever the database happened to return.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<UserInbox>> ListForReconciliationAsync(
        DateTimeOffset staleBefore, int limit, CancellationToken cancellationToken = default) =>
        await Inboxes
            .Include(inbox => inbox.Categories)
            .Where(inbox => inbox.ModifiedAt == null || inbox.ModifiedAt < staleBefore)
            .OrderBy(inbox => inbox.ModifiedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public void Update(UserInbox inbox)
    {
        ArgumentNullException.ThrowIfNull(inbox);

        NotificationAggregateUpdater.MarkDirty(_dbContext, inbox);
    }
}
