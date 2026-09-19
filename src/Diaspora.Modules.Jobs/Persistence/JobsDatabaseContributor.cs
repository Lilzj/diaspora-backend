using Diaspora.Modules.Jobs.Persistence.Configurations;
using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Jobs.Persistence;

/// <summary>
/// Adds the Jobs module's tables to the shared context.
/// <para>
/// Thirty-six tables in the module's own <c>jobs</c> schema, which the context verifies at
/// startup. Twelve configurations, not thirty-six, because twenty-four of those tables are owned
/// by an aggregate and are configured from inside it — the aggregate boundary made visible in
/// the persistence layer: if a table can be reached without loading its root, it should not be
/// owned.
/// </para>
/// <para>
/// <b>Ten of the twelve aggregates are <c>ISoftDeletable</c>, so the shared context applies the
/// global query filter and <c>SoftDeleteConverter</c> handles the owned rows on delete.</b>
/// Nothing here declares either, deliberately — B1.0.1 exists so a module never has to.
/// </para>
/// <para>
/// The two that are not are the catalogues and the pipeline: a category or a skill is retired
/// with a flag rather than deleted, because postings store their keys, and a pipeline dies with
/// the posting that owns it. Analytics is not soft-deletable either — a rollup outlives the
/// posting's visibility, because the employer's response-time reputation is computed from
/// history that must not become unreadable when they archive the advert.
/// </para>
/// </summary>
public sealed class JobsDatabaseContributor : IDatabaseContributor
{
    public string Module => JobsModuleMetadata.Name;

    public string Schema => JobsModuleMetadata.Schema;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // The posting aggregate — owns its detail lines, skills, tags, media, provider and
        // guide references, screening questions and moderation trail.
        modelBuilder.ApplyConfiguration(new JobPostingConfiguration());

        // The two parties. Each owns the collections that describe it and nothing else.
        modelBuilder.ApplyConfiguration(new EmployerProfileConfiguration());
        modelBuilder.ApplyConfiguration(new CandidateProfileConfiguration());

        // The hiring record. Separate aggregates keyed by posting or application — see each
        // for why it is not owned.
        modelBuilder.ApplyConfiguration(new JobApplicationConfiguration());
        modelBuilder.ApplyConfiguration(new InterviewConfiguration());
        modelBuilder.ApplyConfiguration(new JobOfferConfiguration());
        modelBuilder.ApplyConfiguration(new HiringPipelineConfiguration());

        // The candidate's documents.
        modelBuilder.ApplyConfiguration(new ResumeConfiguration());

        // The candidate's own tools.
        modelBuilder.ApplyConfiguration(new SavedJobConfiguration());
        modelBuilder.ApplyConfiguration(new JobReportConfiguration());
        modelBuilder.ApplyConfiguration(new JobAlertConfiguration());

        // The extensible catalogues, seeded by contributors at startup.
        modelBuilder.ApplyConfiguration(new JobCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new JobSkillConfiguration());

        // The measured record.
        modelBuilder.ApplyConfiguration(new JobAnalyticsConfiguration());
    }
}
