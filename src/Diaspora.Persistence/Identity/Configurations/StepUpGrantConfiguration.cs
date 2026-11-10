using Diaspora.Domain.Identity.Verification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Persistence.Identity.Configurations;

/// <summary>
/// Maps <see cref="StepUpGrant"/> — the record that one session re-proved itself (BSEC-007).
/// <para>
/// No row here holds a secret. The code that earned the grant lives only as a hash on the
/// challenge, and this table names the challenge rather than restating anything about it, so a read
/// of this table discloses who confirmed what and when and nothing that could be replayed.
/// </para>
/// </summary>
public sealed class StepUpGrantConfiguration : IEntityTypeConfiguration<StepUpGrant>
{
    public void Configure(EntityTypeBuilder<StepUpGrant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(IdentitySchema.StepUpGrants, IdentitySchema.Name);

        builder.HasKey(grant => grant.Id);

        builder.Property(grant => grant.UserId).IsRequired();
        builder.Property(grant => grant.SessionId).IsRequired();
        builder.Property(grant => grant.ChallengeId).IsRequired();

        // The scope string, not an enum conversion. The catalogue in StepUpScopes will grow, and a
        // row written under a scope a later build has renamed should read back as the string it was
        // granted under rather than fail to materialise — this table is a security ledger and an
        // unreadable row in one is worse than an unrecognised one.
        builder.Property(grant => grant.Scope).HasMaxLength(60).IsRequired();

        builder.Property(grant => grant.GrantedAt).IsRequired();
        builder.Property(grant => grant.ExpiresAt).IsRequired();

        // The gate's only query, in the order it narrows: the member, their session, the scope, and
        // then the expiry it compares. Every guarded request runs this, so it is covered rather
        // than scanned.
        builder.HasIndex(grant => new
            {
                grant.UserId,
                grant.SessionId,
                grant.Scope,
                grant.ExpiresAt
            })
            .HasDatabaseName("ix_step_up_grants_user_session_scope_expires");

        builder.Ignore(grant => grant.DomainEvents);
    }
}
