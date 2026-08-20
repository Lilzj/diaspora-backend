using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.Domain.Identity.Verification;
using Diaspora.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Persistence.Identity.Configurations;

/// <summary>
/// Maps the <see cref="OtpChallenge"/> aggregate. Only the hash of the code is stored.
/// </summary>
public sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(IdentitySchema.OtpChallenges, IdentitySchema.Name);

        builder.HasKey(challenge => challenge.Id);

        builder.Property(challenge => challenge.UserId).IsRequired();

        builder.Property(challenge => challenge.PhoneNumber)
            .HasConversion(phone => phone.Value, value => PhoneNumber.Create(value))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(challenge => challenge.CodeHash)
            .HasConversion(hash => hash.Value, value => SecretHash.Create(value))
            .HasMaxLength(SecretHash.MaxLength)
            .IsRequired();

        builder.Property(challenge => challenge.Purpose).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(challenge => challenge.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(challenge => challenge.IssuedAt).IsRequired();
        builder.Property(challenge => challenge.ExpiresAt).IsRequired();
        builder.Property(challenge => challenge.LastSentAt).IsRequired();
        builder.Property(challenge => challenge.AttemptCount).IsRequired();
        builder.Property(challenge => challenge.MaxAttempts).IsRequired();
        builder.Property(challenge => challenge.ResendCount).IsRequired();
        builder.Property(challenge => challenge.MaxResends).IsRequired();

        // Every OTP lookup is "latest challenge for this user and purpose".
        builder.HasIndex(challenge => new { challenge.UserId, challenge.Purpose, challenge.IssuedAt })
            .HasDatabaseName("ix_otp_challenges_user_purpose_issued");

        builder.HasIndex(challenge => challenge.ExpiresAt)
            .HasDatabaseName("ix_otp_challenges_expires_at");

        builder.Ignore(challenge => challenge.DomainEvents);
    }
}
