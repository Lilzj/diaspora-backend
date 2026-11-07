namespace Diaspora.Contracts.Identity.V1;

/// <summary>A member's current suspension, and the members of staff behind it.</summary>
public sealed record AccountSuspension
{
    public required Guid UserId { get; init; }

    /// <summary>When it ends; null means until reviewed.</summary>
    public DateTimeOffset? Until { get; init; }

    /// <summary>
    /// The member of staff who proposed it. Null for a suspension from before 7 Nov 2026, when one
    /// person could suspend alone and nothing recorded a proposal.
    /// </summary>
    public Guid? ProposedBy { get; init; }

    /// <summary>The member of staff who confirmed it. Null as <see cref="ProposedBy"/> is.</summary>
    public Guid? ConfirmedBy { get; init; }
}

/// <summary>
/// Whether a member is suspended now, and by whom (7 Nov 2026).
/// <para>
/// Trust reviews a member's appeal against their suspension, but the suspension is Identity's, and
/// Trust may not read Identity's tables. It asks here, so that it can refuse an appeal from a member
/// who is not suspended and keep both members of staff behind the suspension from reviewing the
/// appeal against it.
/// </para>
/// </summary>
public interface IAccountSuspensionRecord
{
    /// <summary>The member's suspension, or null when they are not suspended.</summary>
    Task<AccountSuspension?> GetCurrentAsync(Guid userId, CancellationToken cancellationToken = default);
}
