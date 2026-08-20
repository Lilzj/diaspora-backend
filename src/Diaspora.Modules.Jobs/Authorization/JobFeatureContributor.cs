using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Jobs.Authorization;

/// <summary>
/// Declares the feature gates the Jobs module owns.
/// <para>
/// Gates exist so a client can grey out a control with a reason instead of offering it and
/// failing. The split mirrors the permission band: browsing, saving and building a profile are
/// open at <see cref="AccessTier.Limited"/>; applying, posting, recruiting and registering an
/// employer are <see cref="AccessTier.Verified"/>; moderation is staff-only — a gate that told a
/// member they were merely locked out of the moderation queue would imply verifying unlocks it.
/// It does not.
/// </para>
/// <para>
/// <b><c>jobs.browse</c> and <c>jobs.apply</c> keep the exact names and floors the host declared
/// provisionally from B2 onwards</b>, because the mobile client already renders gates against
/// them. The provisional entries are deleted in the same change: the feature catalogue refuses
/// two modules declaring one gate, and the resulting startup failure takes down every request
/// that resolves the catalogue — phone verification included, which is how every integration
/// test creates its member.
/// </para>
/// <para>
/// Constructed by the kernel before the container exists: public parameterless constructor, no
/// dependencies.
/// </para>
/// </summary>
public sealed class JobFeatureContributor : IFeatureContributor
{
    public string Module => JobsModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            JobFeatures.Browse, Module, "Browse and search job listings.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            JobFeatures.Save, Module, "Save jobs and set up job alerts.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            JobFeatures.Profile, Module, "Build a professional profile and upload résumés.")
        {
            MinimumTier = AccessTier.Limited
        };

        // The progression gate the client already knows about. An application puts a real name
        // in front of a stranger who will make a decision about someone's livelihood.
        yield return new FeatureDefinition(
            JobFeatures.Apply, Module, "Apply to a job listing.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            JobFeatures.Employer, Module, "Register and manage an employer profile.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            JobFeatures.Post, Module, "Publish job postings.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            JobFeatures.Recruit, Module, "Review applicants, interview and make offers.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            JobFeatures.Moderate, Module, "Work the jobs moderation queue.")
        {
            MinimumTier = AccessTier.Limited,
            StaffOnly = true
        };
    }
}
