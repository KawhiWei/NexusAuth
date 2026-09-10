using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Persistence.Configurations;

public sealed class WebAuthnCredentialConfiguration : IEntityTypeConfiguration<WebAuthnCredential>
{
    public void Configure(EntityTypeBuilder<WebAuthnCredential> builder)
    {
        builder.ToTable("webauthn_credentials");
        builder.HasKey(credential => credential.Id);
        builder.Property(credential => credential.Id).HasColumnName("id");
        builder.Property(credential => credential.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(credential => credential.CredentialId).HasColumnName("credential_id").HasColumnType("bytea").IsRequired();
        builder.Property(credential => credential.PublicKeyCose).HasColumnName("public_key_cose").HasColumnType("bytea").IsRequired();
        builder.Property(credential => credential.SignatureCounter).HasColumnName("signature_counter").HasConversion<long>().IsRequired();
        builder.Property(credential => credential.Aaguid).HasColumnName("aaguid").IsRequired();
        builder.Property(credential => credential.Transports).HasColumnName("transports").HasColumnType("jsonb").IsRequired();
        builder.Property(credential => credential.IsBackupEligible).HasColumnName("is_backup_eligible").IsRequired();
        builder.Property(credential => credential.IsBackedUp).HasColumnName("is_backed_up").IsRequired();
        builder.Property(credential => credential.DisplayName).HasColumnName("display_name").HasMaxLength(128).IsRequired();
        builder.Property(credential => credential.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(credential => credential.LastUsedAt).HasColumnName("last_used_at");
        builder.Property(credential => credential.DisabledAt).HasColumnName("disabled_at");
        builder.HasIndex(credential => credential.CredentialId).IsUnique().HasDatabaseName("ux_webauthn_credentials_credential_id");
        builder.HasIndex(credential => new { credential.UserId, credential.DisabledAt }).HasDatabaseName("ix_webauthn_credentials_user_enabled");
    }
}
