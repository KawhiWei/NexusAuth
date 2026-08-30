using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusAuth.Domain.AggregateRoots.Users;

namespace NexusAuth.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");

        builder.Property(u => u.Username)
            .HasColumnName("username")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.ExternalId)
            .HasColumnName("external_id")
            .HasMaxLength(256);

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(256);

        builder.Property(u => u.PhoneNumber)
            .HasColumnName("phone_number")
            .HasMaxLength(20);

        builder.Property(u => u.Nickname)
            .HasColumnName("nickname")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.GivenName).HasColumnName("given_name").HasMaxLength(100);
        builder.Property(u => u.FamilyName).HasColumnName("family_name").HasMaxLength(100);
        builder.Property(u => u.MiddleName).HasColumnName("middle_name").HasMaxLength(100);
        builder.Property(u => u.HonorificPrefix).HasColumnName("honorific_prefix").HasMaxLength(50);
        builder.Property(u => u.HonorificSuffix).HasColumnName("honorific_suffix").HasMaxLength(50);
        builder.Property(u => u.ProfileUrl).HasColumnName("profile_url").HasMaxLength(2048);
        builder.Property(u => u.Title).HasColumnName("title").HasMaxLength(256);
        builder.Property(u => u.UserType).HasColumnName("user_type").HasMaxLength(100);
        builder.Property(u => u.PreferredLanguage).HasColumnName("preferred_language").HasMaxLength(35);
        builder.Property(u => u.Locale).HasColumnName("locale").HasMaxLength(35);
        builder.Property(u => u.Timezone).HasColumnName("timezone").HasMaxLength(100);

        builder.Property(u => u.Gender)
            .HasColumnName("gender")
            .HasConversion<short>()
            .IsRequired()
            .HasDefaultValue(Domain.AggregateRoots.Users.Gender.Unknown);

        builder.Property(u => u.Ethnicity)
            .HasColumnName("ethnicity")
            .HasMaxLength(50);

        builder.Property(u => u.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(u => u.IsSystemAccount)
            .HasColumnName("is_system_account")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.FailedLoginAttempts)
            .HasColumnName("failed_login_attempts")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(u => u.LockedUntil)
            .HasColumnName("locked_until");

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(u => u.Username).IsUnique();
        builder.HasIndex(u => u.ExternalId).IsUnique().HasFilter("external_id IS NOT NULL");
        builder.HasIndex(u => u.Email).IsUnique().HasFilter("email IS NOT NULL");
        builder.HasIndex(u => u.PhoneNumber).IsUnique().HasFilter("phone_number IS NOT NULL");
    }
}
