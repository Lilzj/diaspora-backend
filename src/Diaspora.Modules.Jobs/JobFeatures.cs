namespace Diaspora.Modules.Jobs;

/// <summary>
/// The feature gates the Jobs module owns.
/// <para>
/// Dot-separated names with the module prefix. Every gate's floor is at least
/// <c>AccessTier.Limited</c> — a gate below Limited is granted to phone-unverified accounts,
/// which is the B6 lesson made policy. Reading published postings needs no gate at all: a
/// member looking for work must be able to see what work exists before anyone asks them to
/// prove who they are.
/// </para>
/// <para>
/// <b><see cref="Browse"/> and <see cref="Apply"/> are not new names.</b> Both were declared
/// provisionally in the host's <c>ProvisionalCatalogContributor</c> from B2 onwards, against
/// the day this module landed, and the mobile client already renders gates against them. They
/// move here with their floors unchanged — Browse at Limited, Apply at Verified — because a
/// name the client switches on is a wire contract, and the provisional entries are deleted in
/// the same change. Leaving both declarations in place is not harmless: the feature catalogue
/// refuses two modules declaring one gate, and the resulting startup failure takes down every
/// request that resolves the catalogue, phone verification included.
/// </para>
/// </summary>
public static class JobFeatures
{
    /// <summary>Browse and search job listings. The entry point to the whole module.</summary>
    public const string Browse = "jobs.browse";

    /// <summary>
    /// Apply to a job. Verified only — an application carries a real name to a real employer.
    /// </summary>
    public const string Apply = "jobs.apply";

    /// <summary>Save jobs, set alerts and track applications.</summary>
    public const string Save = "jobs.save";

    /// <summary>Build a professional profile and upload résumés.</summary>
    public const string Profile = "jobs.profile";

    /// <summary>Register and manage an employer profile. Verified members only.</summary>
    public const string Employer = "jobs.employer";

    /// <summary>Publish job postings under an employer. Verified members only.</summary>
    public const string Post = "jobs.post";

    /// <summary>Work applicants: shortlist, interview, offer. Verified members only.</summary>
    public const string Recruit = "jobs.recruit";

    /// <summary>Work the jobs moderation queue. Staff only.</summary>
    public const string Moderate = "jobs.moderate";
}
