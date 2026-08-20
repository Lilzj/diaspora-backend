using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Conversations.Domain.ValueObjects;

/// <summary>
/// Somebody is typing, right now, in one conversation.
/// <para>
/// <b>This type is never persisted, and no repository, configuration or migration mentions it.</b>
/// That is not an oversight to be corrected later — it is the contract. Typing is worthless one
/// second after it is true, and a platform that wrote a row per keystroke would be buying write
/// amplification to answer a question nobody asks about the past. B13's runtime already made this
/// decision for the relay; this type is the domain's name for what the relay carries, so a handler
/// can reason about a typing session without reaching for the transport's wire record.
/// </para>
/// <para>
/// It exists as a value object rather than three loose arguments because expiry is a rule —
/// <see cref="HasExpiredAt"/> — and a rule that lives at three call sites is a rule that is
/// eventually applied at two.
/// </para>
/// </summary>
public sealed class TypingSession : ValueObject
{
    private TypingSession(UserId userId, DateTimeOffset startedAt, TimeSpan timeout)
    {
        UserId = userId;
        StartedAt = startedAt;
        Timeout = timeout;
    }

    public UserId UserId { get; }

    public DateTimeOffset StartedAt { get; }

    /// <summary>How long a signal stands before a client should stop rendering it.</summary>
    public TimeSpan Timeout { get; }

    public DateTimeOffset ExpiresAt => StartedAt + Timeout;

    public static TypingSession Start(UserId userId, DateTimeOffset at, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return new TypingSession(
            userId, at, timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(5) : timeout);
    }

    /// <summary>Whether a client should have stopped showing this by now.</summary>
    public bool HasExpiredAt(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>A fresh signal from the same person, which pushes the expiry out.</summary>
    public TypingSession Refresh(DateTimeOffset at) => new(UserId, at, Timeout);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return UserId;
        yield return StartedAt;
        yield return Timeout;
    }
}
