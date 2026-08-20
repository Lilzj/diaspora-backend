using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Housing.Persistence.Configurations;

/// <summary>
/// Maps the landlord aggregate. One table: it owns only value objects.
/// <para>
/// <b>The unique index on the owner is the one-profile-per-member rule</b>, and it is enforced
/// here rather than by a handler remembering to check. A second profile for the same member would
/// leave two rows claiming one person's listings, and no read path could say which was right.
/// </para>
/// </summary>
public sealed class LandlordProfileConfiguration : IEntityTypeConfiguration<LandlordProfile>
{
    public void Configure(EntityTypeBuilder<LandlordProfile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            HousingPersistenceConstants.LandlordsTable, HousingModuleMetadata.Schema);

        builder.HasKey(profile => profile.Id);

        // The optimistic concurrency token, and it has to be declared here rather than merely
        // bumped in the aggregate. EnsureRevision rejects a stale editor that sends back the
        // revision it loaded; nothing stops the editor that does not send one, and two saves from
        // one landlord's two devices would then both commit with the later one silently winning.
        // Declaring the token makes the database arbitrate, which is the only place it can be
        // settled. B10 learnt this; B11 shipped without it until an integration test raced two
        // contexts and found no exception at all.
        builder.Property(profile => profile.Revision).IsConcurrencyToken();

        builder.Property(profile => profile.Id)
            .HasConversion(HousingValueConverters.Id<LandlordProfileId>());

        builder.Property(profile => profile.OwnerUserId)
            .HasConversion(HousingValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(profile => profile.PrimaryCountryCode)
            .HasMaxLength(HousingPersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.Property(profile => profile.Status)
            .HasConversion<string>()
            .HasMaxLength(HousingPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(profile => profile.Bio).HasMaxLength(HousingLimits.MaxDescription);

        builder.Property(profile => profile.SuspensionReason)
            .HasMaxLength(HousingLimits.MaxNoteLength);

        ConfigureIdentity(builder);
        ConfigureSlug(builder);
        ConfigureContact(builder);
        ConfigureTrustMark(builder);
        ConfigureStatistics(builder);

        // One profile per member. See the class summary.
        builder.HasIndex(profile => profile.OwnerUserId)
            .IsUnique()
            .HasDatabaseName("ix_housing_landlords_owner");

        builder.HasIndex(profile => profile.ProviderId)
            .HasDatabaseName("ix_housing_landlords_provider");

        builder.HasIndex(profile => new { profile.PrimaryCountryCode, profile.Status })
            .HasDatabaseName("ix_housing_landlords_country_status");

        builder.Ignore(profile => profile.DomainEvents);
        builder.Ignore(profile => profile.CanLet);
        builder.Ignore(profile => profile.IsVerified);
        builder.Ignore(profile => profile.IsBusiness);
    }

    private static void ConfigureIdentity(EntityTypeBuilder<LandlordProfile> builder)
    {
        builder.OwnsOne(profile => profile.Identity, identity =>
        {
            identity.Property(value => value.DisplayName)
                .HasColumnName("display_name")
                .HasMaxLength(HousingLimits.MaxDisplayName)
                .IsRequired();

            identity.Property(value => value.Kind)
                .HasColumnName("landlord_kind")
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            identity.Property(value => value.LegalName)
                .HasColumnName("legal_name")
                .HasMaxLength(HousingLimits.MaxDisplayName);

            identity.Property(value => value.RegistrationNumber)
                .HasColumnName("registration_number")
                .HasMaxLength(HousingLimits.MaxDisplayName);

            identity.Ignore(value => value.IsBusiness);
            identity.Ignore(value => value.VerifiableName);

            identity.HasIndex(value => value.Kind)
                .HasDatabaseName("ix_housing_landlords_kind");
        });

        builder.Navigation(profile => profile.Identity).IsRequired();
    }

    private static void ConfigureSlug(EntityTypeBuilder<LandlordProfile> builder)
    {
        builder.OwnsOne(profile => profile.Slug, slug =>
        {
            slug.Property(value => value.Value)
                .HasColumnName("slug")
                .HasMaxLength(HousingLimits.MaxSlug)
                .IsRequired();

            slug.HasIndex(value => value.Value)
                .IsUnique()
                .HasDatabaseName("ix_housing_landlords_slug");
        });

        builder.Navigation(profile => profile.Slug).IsRequired();
    }

    private static void ConfigureContact(EntityTypeBuilder<LandlordProfile> builder)
    {
        builder.OwnsOne(profile => profile.Contact, contact =>
        {
            contact.Property(value => value.PhoneNumber)
                .HasColumnName("contact_phone")
                .HasMaxLength(HousingLimits.MaxDisplayName);

            contact.Property(value => value.Email)
                .HasColumnName("contact_email")
                .HasMaxLength(HousingLimits.MaxDisplayName);

            contact.Property(value => value.WebsiteUrl)
                .HasColumnName("contact_website")
                .HasMaxLength(HousingLimits.MaxNoteLength);

            contact.Property(value => value.ShowPublicly).HasColumnName("contact_public");

            contact.Ignore(value => value.IsEmpty);
        });

        builder.Navigation(profile => profile.Contact).IsRequired();
    }

    private static void ConfigureTrustMark(EntityTypeBuilder<LandlordProfile> builder)
    {
        builder.OwnsOne(profile => profile.TrustMark, mark =>
        {
            mark.Property(value => value.State)
                .HasColumnName("verification_state")
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            mark.Property(value => value.VerificationId).HasColumnName("verification_id");

            mark.Property(value => value.Badge)
                .HasColumnName("verification_badge")
                .HasMaxLength(HousingLimits.MaxDisplayName);

            mark.Property(value => value.DecidedAt).HasColumnName("verification_decided_at");

            mark.Property(value => value.Reason)
                .HasColumnName("verification_reason")
                .HasMaxLength(HousingLimits.MaxNoteLength);

            mark.Ignore(value => value.IsVerified);
            mark.Ignore(value => value.IsPending);

            // The correlation key a Trust outcome arrives against.
            mark.HasIndex(value => value.VerificationId)
                .HasDatabaseName("ix_housing_landlords_verification");
        });

        builder.Navigation(profile => profile.TrustMark).IsRequired();
    }

    private static void ConfigureStatistics(EntityTypeBuilder<LandlordProfile> builder)
    {
        builder.OwnsOne(profile => profile.Statistics, statistics =>
        {
            statistics.Property(value => value.ActiveListings).HasColumnName("active_listings");
            statistics.Property(value => value.TotalListings).HasColumnName("total_listings");
            statistics.Property(value => value.CompletedLets).HasColumnName("completed_lets");
            statistics.Property(value => value.ViewingRequests).HasColumnName("viewing_requests");

            statistics.Property(value => value.ViewingsAnswered)
                .HasColumnName("viewings_answered");

            statistics.Property(value => value.NoShowCount).HasColumnName("no_show_count");
            statistics.Property(value => value.LastRespondedAt).HasColumnName("last_responded_at");

            statistics.Ignore(value => value.ResponseRate);
        });

        builder.Navigation(profile => profile.Statistics).IsRequired();
    }
}
