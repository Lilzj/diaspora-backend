namespace Diaspora.Contracts.Identity.V1.Responses;

/// <summary>
/// A member as staff see them.
/// <para>
/// <b>Deliberately not <see cref="UserProfileResponse"/>.</b> That record is what a
/// member sees about themselves and carries everything they are entitled to; this one is
/// what somebody else is entitled to see about them while doing a job. The two will drift
/// as the profile grows, and they should: a moderator answering a report needs the standing
/// of the account, not its preferred time zone.
/// </para>
/// <para>
/// The phone number and email address are here because staff act on accounts they were told
/// about by phone number or email address, and confirming they have the right person is the
/// first thing they do. Nothing about a member's content, location or activity appears.
/// </para>
/// </summary>
public sealed record AdminMemberResponse
{
    public required Guid Id { get; init; }

    public required string FullName { get; init; }

    public required string PhoneNumber { get; init; }

    public string? Email { get; init; }

    /// <summary>The platform role: <c>CommunityUser</c>, <c>Moderator</c>, <c>Admin</c>…</summary>
    public required string Role { get; init; }

    /// <summary>Whether that role is a staff role, so a console need not keep its own list.</summary>
    public required bool IsStaff { get; init; }

    /// <summary><c>Onboarding</c>, <c>Limited</c>, <c>Verified</c>…</summary>
    public required string AccessState { get; init; }

    /// <summary><c>Active</c>, <c>Suspended</c>, <c>Disabled</c>…</summary>
    public required string AccountStatus { get; init; }

    public required bool IsPhoneVerified { get; init; }

    public required string Nationality { get; init; }

    public required string CountryOfResidence { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? LastLoginAt { get; init; }

    /// <summary>Why the account is suspended or closed, when it is.</summary>
    public string? StatusReason { get; init; }

    /// <summary>The moderation reason code of a suspension or closure by staff (7 Nov 2026).</summary>
    public string? StatusReasonCode { get; init; }

    /// <summary>When a suspension lifts. Null on an indefinite one, which is heavier.</summary>
    public DateTimeOffset? SuspendedUntil { get; init; }
}
