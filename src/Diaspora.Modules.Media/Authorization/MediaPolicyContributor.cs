using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Media.Authorization;

/// <summary>
/// Declares the policies the module's endpoints reference.
/// <para>
/// The kernel validates at startup that every permission named here exists, so a typo fails
/// the boot rather than silently denying a route forever.
/// </para>
/// </summary>
public sealed class MediaPolicyContributor : IPolicyContributor
{
    public string Module => MediaModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition.ForPermission(
            MediaPolicies.Read, Module, MediaPermissions.Read);

        yield return PolicyDefinition.ForPermission(
            MediaPolicies.UploadImage, Module, MediaPermissions.UploadImage);

        yield return PolicyDefinition.ForPermission(
            MediaPolicies.UploadDocument, Module, MediaPermissions.UploadDocument);

        yield return PolicyDefinition.ForPermission(
            MediaPolicies.UploadAudio, Module, MediaPermissions.UploadAudio);

        yield return PolicyDefinition.ForPermission(
            MediaPolicies.UploadVideo, Module, MediaPermissions.UploadVideo);

        yield return PolicyDefinition.ForPermission(
            MediaPolicies.Manage, Module, MediaPermissions.Manage);

        yield return PolicyDefinition.ForPermission(
            MediaPolicies.Delete, Module, MediaPermissions.Delete);

        yield return PolicyDefinition.ForPermission(
            MediaPolicies.ManageCollections, Module, MediaPermissions.ManageCollections);

        yield return PolicyDefinition.ForPermission(
            MediaPolicies.Transform, Module, MediaPermissions.Transform);

        yield return PolicyDefinition.ForPermission(
            MediaPolicies.SignedAccess, Module, MediaPermissions.SignedAccess);

        yield return PolicyDefinition.ForPermission(
            MediaPolicies.Administer, Module, MediaPermissions.Administer);

        yield return PolicyDefinition.ForPermission(
            MediaPolicies.ManageProcessing, Module, MediaPermissions.ManageProcessing);
    }
}
