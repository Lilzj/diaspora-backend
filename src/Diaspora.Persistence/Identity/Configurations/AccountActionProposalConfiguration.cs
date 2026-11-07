using Diaspora.Domain.Identity.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Persistence.Identity.Configurations;

/// <summary>
/// Maps <see cref="AccountActionProposal"/>: account actions one member of staff proposes and a
/// second decides (7 Nov 2026).
/// </summary>
public sealed class AccountActionProposalConfiguration : IEntityTypeConfiguration<AccountActionProposal>
{
    public void Configure(EntityTypeBuilder<AccountActionProposal> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(IdentitySchema.AccountActionProposals, IdentitySchema.Name);

        builder.HasKey(proposal => proposal.Id);

        // Declared in the domain, not the shared kernel, so the convention that converts the
        // kernel's ids does not reach it.
        builder.Property(proposal => proposal.Id)
            .HasConversion(new StronglyTypedIdValueConverter<AccountActionProposalId>());

        builder.Property(proposal => proposal.Kind)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(proposal => proposal.TargetUserId).IsRequired();
        builder.Property(proposal => proposal.ProposedBy).IsRequired();

        builder.Property(proposal => proposal.ReasonCode)
            .HasMaxLength(AccountActionProposal.MaxReasonCodeLength);

        builder.Property(proposal => proposal.Note)
            .HasMaxLength(AccountActionProposal.MaxNoteLength)
            .IsRequired();

        builder.Property(proposal => proposal.Role)
            .HasMaxLength(AccountActionProposal.MaxRoleLength);

        builder.Property(proposal => proposal.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(proposal => proposal.DecisionNote)
            .HasMaxLength(AccountActionProposal.MaxDecisionNoteLength);

        builder.Property(proposal => proposal.CreatedAt).IsRequired();

        // The queue staff work from: what is waiting, newest first.
        builder.HasIndex(proposal => new { proposal.Status, proposal.CreatedAt })
            .HasDatabaseName("ix_account_action_proposals_status_created");

        // "Is this already proposed for this account?", asked before every new proposal.
        builder.HasIndex(proposal => new { proposal.TargetUserId, proposal.Kind, proposal.Status })
            .HasDatabaseName("ix_account_action_proposals_target_kind_status");

        builder.Ignore(proposal => proposal.IsPending);
        builder.Ignore(proposal => proposal.DomainEvents);
    }
}
