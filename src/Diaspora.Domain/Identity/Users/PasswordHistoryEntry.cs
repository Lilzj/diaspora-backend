using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Users;

/// <summary>
/// A retired password hash, kept so a member cannot cycle straight back to a
/// password they just abandoned. The aggregate trims the collection to the
/// configured depth on every change.
/// </summary>
public sealed class PasswordHistoryEntry : Entity<PasswordHistoryEntryId>
{
    public UserId UserId { get; private set; } = default!;
    public PasswordHash Hash { get; private set; } = default!;
    public DateTimeOffset RetiredAt { get; private set; }

    private PasswordHistoryEntry() { }

    private PasswordHistoryEntry(
        PasswordHistoryEntryId id,
        UserId userId,
        PasswordHash hash,
        DateTimeOffset retiredAt) : base(id)
    {
        UserId = userId;
        Hash = hash;
        RetiredAt = retiredAt;
    }

    public static PasswordHistoryEntry Create(UserId userId, PasswordHash hash, DateTimeOffset retiredAt) =>
        new(PasswordHistoryEntryId.New(), userId, hash, retiredAt);
}
