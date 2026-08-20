using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Conversations.Authorization;

/// <summary>
/// What a caller must hold, and at which access tier.
/// <para>
/// <b>Reading and participating are <see cref="AccessTier.Limited"/>, not Verified.</b> A member who
/// has just registered and wants to ask about a room is exactly the person this platform exists for,
/// and requiring verification first would make the first useful thing they can do the thing they
/// cannot. Trust still governs whether they are believed — a verification badge is rendered beside
/// them — but not whether they may speak.
/// </para>
/// <para>
/// <b>Moderation is staff.</b> Reading a conversation you are not in is the single most invasive
/// thing this platform permits, and it is gated behind a permission no ordinary tier grants.
/// </para>
/// </summary>
public sealed class ConversationPermissionContributor : IPermissionContributor
{
    /// <summary>
    /// Who may read a conversation they are not in.
    /// <para>
    /// A role list rather than a tier, because tiers are a member's own progression and this is a
    /// privilege granted to staff — B2.0.1's split. <c>Verified</c> is the top tier and means "a
    /// contributor", which is emphatically not the same as "may read other people's messages".
    /// </para>
    /// </summary>
    private static readonly HashSet<string> StaffRoles =
        new(StringComparer.Ordinal) { "Moderator", "Admin", "SuperAdmin" };

    public string Module => ConversationsModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        yield return new PermissionDefinition(
            ConversationPermissions.Read,
            Module,
            "Read your own conversations and their messages.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            ConversationPermissions.Participate,
            Module,
            "Open conversations, send messages, and manage your own inbox.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            ConversationPermissions.Moderate,
            Module,
            "Read and close conversations you are not a participant in. Staff only.")
        {
            Roles = StaffRoles
        };
    }
}

public sealed class ConversationFeatureContributor : IFeatureContributor
{
    public string Module => ConversationsModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            ConversationFeatures.Use,
            Module,
            "Message other members about listings, rooms, jobs and events.")
        {
            MinimumTier = AccessTier.Limited
        };

        // Separate, because attachments cost storage and a deployment may want text-only
        // conversations before Media is provisioned.
        yield return new FeatureDefinition(
            ConversationFeatures.Attachments,
            Module,
            "Send photographs, documents and voice notes in a conversation.")
        {
            MinimumTier = AccessTier.Limited
        };
    }
}

public sealed class ConversationPolicyContributor : IPolicyContributor
{
    public string Module => ConversationsModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition.ForPermission(
            ConversationPolicies.Read, Module, ConversationPermissions.Read);

        yield return PolicyDefinition.ForPermission(
            ConversationPolicies.Participate, Module, ConversationPermissions.Participate);

        yield return PolicyDefinition.ForPermission(
            ConversationPolicies.Moderate, Module, ConversationPermissions.Moderate);
    }
}
