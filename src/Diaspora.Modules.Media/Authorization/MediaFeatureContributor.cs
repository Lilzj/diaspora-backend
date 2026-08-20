using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Media.Authorization;

/// <summary>
/// Declares the feature gates the Media module owns.
/// <para>
/// Gates exist so the client can grey out a control with a reason rather than offering it and
/// failing. That matters more here than almost anywhere else on the platform: a member who
/// selects a 400 MB video, waits for it to upload over a mobile connection, and is then told
/// their tier does not permit video has had the worst possible version of this interaction.
/// The gate lets the button say so before the picker opens.
/// </para>
/// </summary>
public sealed class MediaFeatureContributor : IFeatureContributor
{
    public string Module => MediaModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            MediaFeatures.Upload, Module, "Upload files.")
        {
            MinimumTier = AccessTier.Onboarding
        };

        yield return new FeatureDefinition(
            MediaFeatures.PhotoUpload, Module, "Add photographs.")
        {
            MinimumTier = AccessTier.Onboarding
        };

        yield return new FeatureDefinition(
            MediaFeatures.DocumentUpload, Module, "Attach documents.")
        {
            MinimumTier = AccessTier.Onboarding
        };

        yield return new FeatureDefinition(
            MediaFeatures.AudioUpload, Module, "Record voice notes.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            MediaFeatures.VideoUpload, Module, "Add videos.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            MediaFeatures.ResumableUpload, Module, "Pause and resume large uploads.")
        {
            MinimumTier = AccessTier.Onboarding
        };

        yield return new FeatureDefinition(
            MediaFeatures.Galleries, Module, "Group and reorder photographs.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            MediaFeatures.Transformations, Module, "Request different sizes of an image.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            MediaFeatures.SignedUrls, Module, "Open private files through short-lived links.")
        {
            MinimumTier = AccessTier.Onboarding
        };
    }
}
