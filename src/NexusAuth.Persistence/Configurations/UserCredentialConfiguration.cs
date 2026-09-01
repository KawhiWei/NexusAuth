using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Persistence.Configurations;

public sealed class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
{
    public void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        builder.ToTable("user_credentials");
        builder.HasKey(credential => credential.Id);
        builder.Property(credential => credential.Id).HasColumnName("id");
        builder.Property(credential => credential.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(credential => credential.Type).HasColumnName("type").HasMaxLength(32).IsRequired();
        builder.Property(credential => credential.DisplayName).HasColumnName("display_name").HasMaxLength(128).IsRequired();
        builder.Property(credential => credential.SecretProtected).HasColumnName("secret_protected").HasColumnType("text");
        builder.Property(credential => credential.PendingSecretProtected).HasColumnName("pending_secret_protected").HasColumnType("text");
        builder.Property(credential => credential.PendingExpiresAt).HasColumnName("pending_expires_at");
        builder.Property(credential => credential.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(false).IsRequired();
        builder.Property(credential => credential.LastUsedCounter).HasColumnName("last_used_counter");
        builder.Property(credential => credential.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(credential => credential.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(credential => credential.DisabledAt).HasColumnName("disabled_at");
        builder.HasIndex(credential => new { credential.UserId, credential.Type, credential.IsEnabled })
            .HasDatabaseName("ix_user_credentials_user_type_enabled");
        builder.ToTable(table => table.HasCheckConstraint(
            "ck_user_credentials_totp_state",
            "(type <> 'totp') OR ((is_enabled = false) OR (secret_protected IS NOT NULL))"));
    }
}
