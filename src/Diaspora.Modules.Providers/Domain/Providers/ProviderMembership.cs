using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Providers;

/// <summary>
/// One person's authority over one provider.
/// <para>
/// <b>The whole of B7's ownership model, and deliberately a list rather than an owner column.</b>
/// The brief requires a single owner, multiple managers, delegated editors and a path to
/// franchises; an <c>OwnerId</c> on the aggregate gives the first and forecloses the rest. A
/// membership list gives all four, and the "single owner" case is simply a list of one.
/// </para>
/// <para>
/// The invariant that a provider always has at least one <see cref="ProviderRole.Owner"/> lives
/// on the aggregate, not here — an entity cannot see its siblings, and enforcing it from inside
/// a single membership is how you get a provider with no owner after two concurrent removals.
/// </para>
/// <para>
/// <see cref="UserId"/> is Identity's, held as a value and never as a foreign key. B7 does not
/// reference the Identity module; it stores the identifier the kernel's <c>ICurrentUser</c>
/// hands it, exactly as B6 does for authorship. A member who is deleted from Identity leaves a
/// membership that resolves to nothing, which the read path renders as an unknown person rather
/// than failing — the alternative, a real FK, would make deleting a member a cascade across
/// every provider they ever helped run.
/// </para>
/// </summary>
public sealed class ProviderMembership : Entity<ProviderMembershipId>
{
    public const int MaxTitleLength = 120;

    private ProviderMembership(
        ProviderMembershipId id,
        UserId userId,
        ProviderRole role,
        DateTimeOffset joinedAt,
        UserId? invitedBy,
        string? title)
        : base(id)
    {
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
        InvitedBy = invitedBy;
        Title = title;
    }

    private ProviderMembership() { }

    /// <summary>Identity's user identifier. A value, not a foreign key.</summary>
    public UserId UserId { get; private set; } = default!;

    public ProviderRole Role { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    /// <summary>Who added them. Null for the founding owner, who added themselves.</summary>
    public UserId? InvitedBy { get; private set; }

    /// <summary>
    /// A human label the provider chooses. "Head Pastor", "Practice Manager", "Owner &amp; Chef".
    /// The client's <c>OrgLeader</c> renders this, and it is not the same thing as
    /// <see cref="Role"/> — a "Head Pastor" may be an <see cref="ProviderRole.Editor"/>.
    /// </summary>
    public string? Title { get; private set; }

    /// <summary>Whether this person is shown publicly on the provider's profile.</summary>
    public bool IsPubliclyListed { get; private set; }

    public static ProviderMembership Create(
        ProviderMembershipId id,
        UserId userId,
        ProviderRole role,
        DateTimeOffset joinedAt,
        UserId? invitedBy = null,
        string? title = null,
        bool isPubliclyListed = false)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(userId);

        return new ProviderMembership(id, userId, role, joinedAt, invitedBy, Trim(title))
        {
            IsPubliclyListed = isPubliclyListed
        };
    }

    /// <summary>
    /// Changes the role. Internal because every legitimate caller goes through the aggregate,
    /// which is the only place that can check the last-owner invariant.
    /// </summary>
    internal void ChangeRole(ProviderRole role) => Role = role;

    public void Describe(string? title, bool isPubliclyListed)
    {
        Title = Trim(title);
        IsPubliclyListed = isPubliclyListed;
    }

    /// <summary>Whether this membership carries at least the given authority.</summary>
    public bool HasAtLeast(ProviderRole role) => Role >= role;

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, MaxTitleLength)];
}
