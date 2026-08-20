namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// One person's role on a provider.
/// <para>
/// <see cref="Role"/> serialises lower-case — <c>owner</c>, <c>manager</c>, <c>moderator</c>,
/// <c>contributor</c> — because the client compares those literals directly and keys its
/// role-tab filter on them.
/// </para>
/// </summary>
public sealed record ProviderMemberResponse
{
    public required Guid MembershipId { get; init; }

    public required Guid UserId { get; init; }

    /// <summary>Lower-case, matching the client's <c>OrganizationRole</c> union.</summary>
    public required string Role { get; init; }

    /// <summary>The provider's own label. "Head Pastor", "Practice Manager".</summary>
    public string? Title { get; init; }

    public required bool IsPubliclyListed { get; init; }

    public required DateTimeOffset JoinedAt { get; init; }
}
