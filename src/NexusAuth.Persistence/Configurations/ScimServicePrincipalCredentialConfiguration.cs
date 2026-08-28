using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Persistence.Configurations;

public class ScimServicePrincipalCredentialConfiguration
    : IEntityTypeConfiguration<ScimServicePrincipalCredential>
{
    public void Configure(EntityTypeBuilder<ScimServicePrincipalCredential> builder)
    {
        // Keep the same explicit converter used by OAuthClient so this remains
        // compatible with Npgsql versions that do not enable dynamic JSON.
        var stringListConverter = new ValueConverter<List<string>, string>(
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
            value => JsonSerializer.Deserialize<List<string>>(value, (JsonSerializerOptions?)null) ?? new List<string>());

        var stringListComparer = new ValueComparer<List<string>>(
            (left, right) => JsonSerializer.Serialize(left, (JsonSerializerOptions?)null)
                == JsonSerializer.Serialize(right, (JsonSerializerOptions?)null),
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null).GetHashCode(),
            value => JsonSerializer.Deserialize<List<string>>(
                JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                (JsonSerializerOptions?)null) ?? new List<string>());

        builder.ToTable("scim_service_principal_credentials");

        builder.HasKey(credential => credential.Id);
        builder.Property(credential => credential.Id)
            .HasColumnName("id");

        builder.Property(credential => credential.Name)
            .HasColumnName("name")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(credential => credential.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(ScimServicePrincipalCredential.TokenHashLength)
            .IsRequired();

        builder.Property(credential => credential.Scopes)
            .HasColumnName("scopes")
            .HasColumnType("jsonb")
            .HasConversion(stringListConverter)
            .Metadata.SetValueComparer(stringListComparer);

        builder.Property(credential => credential.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(credential => credential.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(credential => credential.LastUsedAt)
            .HasColumnName("last_used_at");

        builder.Property(credential => credential.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(credential => credential.RevokedAt)
            .HasColumnName("revoked_at");

        builder.HasIndex(credential => credential.Name)
            .HasDatabaseName("ix_scim_service_principal_credentials_name")
            .IsUnique();

        builder.HasIndex(credential => credential.TokenHash)
            .HasDatabaseName("ix_scim_service_principal_credentials_token_hash")
            .IsUnique();

        builder.ToTable(table => table.HasCheckConstraint(
            "ck_scim_service_principal_credentials_token_hash_base64url",
            "token_hash ~ '^[A-Za-z0-9_-]{43}$'"));
    }
}
