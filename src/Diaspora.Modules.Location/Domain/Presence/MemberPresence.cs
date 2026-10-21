using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Presence;

/// <summary>
/// A member's "when the app is open" location setting, and the one fact it keeps — 21 Oct 2026.
/// <para>
/// <b>Off unless the member turns it on.</b> With it off the server takes no location at all: a ping
/// is refused, and nothing is remembered.
/// </para>
/// <para>
/// <b>No history, and no coordinates.</b> The app reports a point while it is in the foreground; the
/// server resolves it to a catalogue area and keeps <i>only that area's identifier</i> — the coarse
/// cell it needs to notice the member arriving somewhere new. The point itself is never stored,
/// and the previous area is overwritten, not appended: there is one row per member and it never
/// grows. Turning the setting off forgets the area too, so switching it back on starts clean rather
/// than comparing against wherever the member was weeks ago.
/// </para>
/// </summary>
public sealed class MemberPresence : Entity<MemberPresenceId>
{
    private MemberPresence() { }

    private MemberPresence(MemberPresenceId id, UserId userId) : base(id)
    {
        UserId = userId;
    }

    public UserId UserId { get; private set; } = default!;

    public bool IsEnabled { get; private set; }

    /// <summary>When the member last switched it on. Null while it is off.</summary>
    public DateTimeOffset? EnabledAt { get; private set; }

    /// <summary>
    /// The most specific catalogue area the last evaluated report resolved to, or null when it was
    /// in no area the catalogue holds, or nothing has been evaluated since it was switched on.
    /// </summary>
    public Guid? LastAreaId { get; private set; }

    /// <summary>
    /// When a report was last evaluated. Null until the first one after switching on, which is the
    /// baseline rather than an arrival: a member who turns this on at home has not just arrived there.
    /// </summary>
    public DateTimeOffset? LastEvaluatedAt { get; private set; }

    public DateTimeOffset ModifiedAt { get; private set; }

    public static MemberPresence For(UserId userId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return new MemberPresence(MemberPresenceId.New(), userId) { ModifiedAt = now };
    }

    /// <summary>Whether this is the first report since the setting was switched on.</summary>
    public bool IsBaseline => LastEvaluatedAt is null;

    /// <summary>Switches it on or off. Off forgets the last area.</summary>
    public void SetEnabled(bool enabled, DateTimeOffset now)
    {
        if (enabled == IsEnabled)
        {
            return;
        }

        IsEnabled = enabled;
        EnabledAt = enabled ? now : null;
        LastAreaId = null;
        LastEvaluatedAt = null;
        ModifiedAt = now;
    }

    /// <summary>Remembers where the latest report landed, replacing the previous answer.</summary>
    public void Record(Guid? areaId, DateTimeOffset now)
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("A presence report is not recorded while it is off.");
        }

        LastAreaId = areaId;
        LastEvaluatedAt = now;
        ModifiedAt = now;
    }
}
