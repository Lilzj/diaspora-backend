using Diaspora.Domain.Identity.Sessions;
using Diaspora.Domain.Identity.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Persistence.Identity.Configurations;

/// <summary>
/// Maps <see cref="RefreshToken"/> as a first-class entity rather than an owned collection.
/// <para>
/// It remains part of the <c>UserSession</c> aggregate conceptually — only the session
/// mutates it — but every refresh request begins by finding a token from its hash alone,
/// with no session id in hand. Owned types cannot be queried independently, so an owned
/// mapping would force loading every session to find one token.
/// </para>
/// </summary>
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(IdentitySchema.RefreshTokens, IdentitySchema.Name);

        builder.HasKey(token => token.Id);

        builder.Property(token => token.SessionId).IsRequired();

        builder.Property(token => token.TokenHash)
            .HasConversion(hash => hash.Value, value => SecretHash.Create(value))
            .HasMaxLength(SecretHash.MaxLength)
            .IsRequired();

        builder.Property(token => token.CreatedAt).IsRequired();
        builder.Property(token => token.ExpiresAt).IsRequired();

        // The concurrency token, and the reason rotation is safe under load.
        //
        // Two requests presenting the same refresh token both load a session whose token
        // shows RotatedAt IS NULL, both decide the token is unspent, and both write. B2 had
        // no concurrency control, so the second write simply overwrote the first: two live
        // replacement tokens where the design guarantees one, and a reuse-detection
        // guarantee that quietly stopped holding under exactly the conditions — mobile
        // clients, flaky networks, parallel requests — that make it matter.
        //
        // Marking RotatedAt as a concurrency token puts `AND rotated_at IS NULL` into the
        // UPDATE. The loser matches zero rows and gets a DbUpdateConcurrencyException, which
        // the refresh handler treats as a replay. Chosen over a rowversion column because it
        // needs no schema change, no provider-specific type, and expresses the invariant
        // directly: a token may be spent exactly once.
        builder.Property(token => token.RotatedAt).IsConcurrencyToken();

        builder.HasIndex(token => token.ExpiresAt)
            .HasDatabaseName("ix_refresh_tokens_expires_at");

        // The lookup on the critical path of every token refresh. Unique, because a
        // collision would let one member's token resolve to another member's session.
        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("ix_refresh_tokens_token_hash");

        builder.HasIndex(token => token.SessionId)
            .HasDatabaseName("ix_refresh_tokens_session_id");

        builder.Ignore(token => token.DomainEvents);
    }
}
