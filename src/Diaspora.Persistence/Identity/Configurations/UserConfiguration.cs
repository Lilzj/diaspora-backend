using Diaspora.Domain.Identity.Users;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Persistence.Identity.Configurations;

/// <summary>
/// Maps the <see cref="User"/> aggregate.
/// <para>
/// Value objects are flattened into columns via converters rather than owned types where
/// they hold a single value — a one-column owned entity buys nothing and complicates
/// queries. The security profile is genuinely multi-field, so it is owned.
/// </para>
/// <para>
/// No provider-specific column types appear here. The module targets PostgreSQL in
/// production but the integration suite runs on SQLite, and a <c>jsonb</c> or
/// <c>timestamptz</c> literal would make the two diverge.
/// </para>
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(IdentitySchema.Users, IdentitySchema.Name);

        builder.HasKey(user => user.Id);

        builder.Property(user => user.FullName)
            .HasConversion(name => name.Value, value => FullName.Create(value))
            .HasMaxLength(FullName.MaxLength)
            .IsRequired();

        builder.Property(user => user.PhoneNumber)
            .HasConversion(phone => phone.Value, value => PhoneNumber.Create(value))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(user => user.Email)
            .HasConversion(
                email => email == null ? null : email.Value,
                value => value == null ? null : EmailAddress.Create(value))
            .HasMaxLength(320);

        builder.Property(user => user.Nationality)
            .HasConversion(code => code.Value, value => CountryCode.Create(value))
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(user => user.CountryOfResidence)
            .HasConversion(code => code.Value, value => CountryCode.Create(value))
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(user => user.PreferredLanguage)
            .HasConversion(code => code.Value, value => LanguageCode.Create(value))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(user => user.PreferredTimeZone)
            .HasConversion(zone => zone.Value, value => TimeZoneIdentifier.Create(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(user => user.Avatar)
            .HasConversion(
                avatar => avatar == null ? null : avatar.StorageKey,
                value => value == null ? null : AvatarReference.Create(value))
            .HasMaxLength(AvatarReference.MaxLength);

        builder.Property(user => user.PasswordHash)
            .HasConversion(hash => hash.Value, value => PasswordHash.Create(value))
            .HasMaxLength(PasswordHash.MaxLength)
            .IsRequired();

        builder.Property(user => user.Role)
            .HasConversion(new UserRoleConverter())
            .HasMaxLength(UserRoleConverter.MaxLength)
            .IsRequired();

        builder.Property(user => user.AccessState)
            .HasConversion(new AccessStateConverter())
            .HasMaxLength(AccessStateConverter.MaxLength)
            .IsRequired();

        builder.Property(user => user.PreviousAccessState)
            .HasConversion(new NullableAccessStateConverter())
            .HasMaxLength(AccessStateConverter.MaxLength);

        builder.Property(user => user.StatusReason)
            .HasMaxLength(User.MaxStatusReasonLength);

        builder.OwnsOne(user => user.Security, security =>
        {
            security.Property(profile => profile.FailedLoginAttempts)
                .HasColumnName("failed_login_attempts")
                .IsRequired();

            security.Property(profile => profile.LastFailedLoginAt).HasColumnName("last_failed_login_at");
            security.Property(profile => profile.LockoutEndsAt).HasColumnName("lockout_ends_at");
            security.Property(profile => profile.PasswordChangedAt).HasColumnName("password_changed_at");
            security.Property(profile => profile.PhoneVerifiedAt).HasColumnName("phone_verified_at");
            security.Property(profile => profile.EmailVerifiedAt).HasColumnName("email_verified_at");

            security.Property(profile => profile.RequiresPasswordChange)
                .HasColumnName("requires_password_change")
                .IsRequired();

            security.Property(profile => profile.SecurityStamp)
                .HasColumnName("security_stamp")
                .IsRequired();
        });

        builder.Navigation(user => user.Security).IsRequired();

        builder.OwnsMany(user => user.PasswordHistory, history =>
        {
            history.ToTable(IdentitySchema.PasswordHistory, IdentitySchema.Name);

            history.HasKey(entry => entry.Id);

            history.WithOwner().HasForeignKey(entry => entry.UserId);

            history.Property(entry => entry.Hash)
                .HasConversion(hash => hash.Value, value => PasswordHash.Create(value))
                .HasMaxLength(PasswordHash.MaxLength)
                .IsRequired();

            history.Property(entry => entry.RetiredAt).IsRequired();

            history.HasIndex(entry => new { entry.UserId, entry.RetiredAt });
        });

        builder.Metadata
            .FindNavigation(nameof(User.PasswordHistory))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Phone number is the sign-in credential, so uniqueness is a correctness
        // requirement rather than a nicety.
        //
        // KNOWN LIMITATION (tracked): this index is unfiltered, so a soft-deleted account
        // permanently reserves its phone number — a closed account blocks re-registration of
        // a number a carrier may later reassign. The B2.5 audit flagged it (DB-01). The fix
        // is a partial unique index (WHERE IsDeleted = false), but the filter SQL differs
        // between PostgreSQL and the SQLite the integration suite runs on, and the Postgres
        // migration cannot be verified from the current environment. Left as unfiltered and
        // documented rather than shipped unverified; carried in the B3 backlog with the
        // provider-split filter and a migration.
        builder.HasIndex(user => user.PhoneNumber)
            .IsUnique()
            .HasDatabaseName("ix_users_phone_number");

        builder.HasIndex(user => user.Email)
            .HasDatabaseName("ix_users_email");

        builder.HasIndex(user => user.AccessState)
            .HasDatabaseName("ix_users_access_state");

        builder.HasIndex(user => user.IsDeleted)
            .HasDatabaseName("ix_users_is_deleted");

        builder.Ignore(user => user.AccountStatus);
        builder.Ignore(user => user.DomainEvents);
    }
}
