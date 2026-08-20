using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(43)
            .IsRequired();

        builder.Property(r => r.ClientId)
            .HasColumnName("client_id")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(r => r.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(r => r.Scope)
            .HasColumnName("scope")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(r => r.IsRevoked)
            .HasColumnName("is_revoked")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(r => r.TokenHash)
            .HasDatabaseName("ix_refresh_tokens_token_hash")
            .IsUnique();

        builder.HasIndex(r => new { r.TokenHash, r.ClientId, r.IsRevoked, r.ExpiresAt })
            .HasDatabaseName("ix_refresh_tokens_rotate");

        builder.ToTable(table => table.HasCheckConstraint(
            "ck_refresh_tokens_token_hash_base64url",
            "token_hash ~ '^[A-Za-z0-9_-]{43}$'"));
    }
}
