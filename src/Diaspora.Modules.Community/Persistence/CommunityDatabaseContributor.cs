using Diaspora.Modules.Community.Persistence.Configurations;
using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Community.Persistence;

/// <summary>
/// Adds the Community module's tables to the shared context.
/// <para>
/// Twenty-six tables in the module's own <c>community</c> schema, which the context verifies at
/// startup. The boundary matters here for a reason particular to this module: Community is the
/// one every later content module reads through, so a table of somebody else's leaking into this
/// schema would be a dependency nobody could see in a project reference.
/// </para>
/// </summary>
public sealed class CommunityDatabaseContributor : IDatabaseContributor
{
    public string Module => CommunityModuleMetadata.Name;

    public string Schema => CommunityModuleMetadata.Schema;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Posts — owns attachments, hashtag links, mention spans, revisions, the poll and its
        // options.
        modelBuilder.ApplyConfiguration(new CommunityPostConfiguration());

        // Conversation — owns comment mention spans.
        modelBuilder.ApplyConfiguration(new CommentConfiguration());

        // Reactions: instances, per-content summaries with their tallies, and the catalogue.
        modelBuilder.ApplyConfiguration(new ReactionConfiguration());
        modelBuilder.ApplyConfiguration(new ReactionSummaryConfiguration());
        modelBuilder.ApplyConfiguration(new ReactionDefinitionConfiguration());

        modelBuilder.ApplyConfiguration(new ShareConfiguration());
        modelBuilder.ApplyConfiguration(new BookmarkConfiguration());
        modelBuilder.ApplyConfiguration(new BookmarkFolderConfiguration());
        modelBuilder.ApplyConfiguration(new FollowConfiguration());
        modelBuilder.ApplyConfiguration(new HashtagConfiguration());
        modelBuilder.ApplyConfiguration(new MentionConfiguration());

        // Moderation: reports, the append-only decision log, automated reviews.
        modelBuilder.ApplyConfiguration(new ContentReportConfiguration());
        modelBuilder.ApplyConfiguration(new ModerationDecisionConfiguration());
        modelBuilder.ApplyConfiguration(new ContentReviewConfiguration());

        modelBuilder.ApplyConfiguration(new VisibilityRuleConfiguration());
        modelBuilder.ApplyConfiguration(new FeedSubscriptionConfiguration());
        modelBuilder.ApplyConfiguration(new PollVoteConfiguration());
    }
}
