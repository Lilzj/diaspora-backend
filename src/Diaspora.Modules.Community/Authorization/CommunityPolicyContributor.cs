using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Community.Authorization;

/// <summary>
/// Declares the policies the module's endpoints reference.
/// <para>
/// The kernel validates at startup that every permission named here exists, so a typo fails the
/// boot rather than silently denying a route forever.
/// </para>
/// </summary>
public sealed class CommunityPolicyContributor : IPolicyContributor
{
    public string Module => CommunityModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition.ForPermission(
            CommunityPolicies.Read, Module, CommunityPermissions.Read);

        yield return PolicyDefinition.ForPermission(
            CommunityPolicies.Post, Module, CommunityPermissions.Post);

        yield return PolicyDefinition.ForPermission(
            CommunityPolicies.ManagePost, Module, CommunityPermissions.ManagePost);

        yield return PolicyDefinition.ForPermission(
            CommunityPolicies.Comment, Module, CommunityPermissions.Comment);

        yield return PolicyDefinition.ForPermission(
            CommunityPolicies.ManageComment, Module, CommunityPermissions.ManageComment);

        yield return PolicyDefinition.ForPermission(
            CommunityPolicies.React, Module, CommunityPermissions.React);

        yield return PolicyDefinition.ForPermission(
            CommunityPolicies.Share, Module, CommunityPermissions.Share);

        yield return PolicyDefinition.ForPermission(
            CommunityPolicies.Bookmark, Module, CommunityPermissions.Bookmark);

        yield return PolicyDefinition.ForPermission(
            CommunityPolicies.Follow, Module, CommunityPermissions.Follow);

        yield return PolicyDefinition.ForPermission(
            CommunityPolicies.Poll, Module, CommunityPermissions.Poll);

        yield return PolicyDefinition.ForPermission(
            CommunityPolicies.Report, Module, CommunityPermissions.Report);

        yield return PolicyDefinition.ForPermission(
            CommunityPolicies.ModerationRead, Module, CommunityPermissions.ModerationRead);

        yield return PolicyDefinition.ForPermission(
            CommunityPolicies.Moderate, Module, CommunityPermissions.Moderate);

        yield return PolicyDefinition.ForPermission(
            CommunityPolicies.Pin, Module, CommunityPermissions.Pin);

        yield return PolicyDefinition.ForPermission(
            CommunityPolicies.Administer, Module, CommunityPermissions.Administer);
    }
}
