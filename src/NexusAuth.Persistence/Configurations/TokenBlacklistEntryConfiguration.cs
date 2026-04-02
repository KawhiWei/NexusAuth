using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Persistence.Configurations;

public class TokenBlacklistEntryConfiguration : IEntityTypeConfiguration<TokenBlacklistEntry>
{
    public void Configure(EntityTypeBuilder<TokenBlacklistEntry> builder)
    {
        builder.ToTable("token_blacklist_entries");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.Jti)
            .HasColumnName("jti")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(t => t.TokenType)
            .HasColumnName("token_type")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(t => t.Subject)
            .HasColumnName("subject")
            .HasMaxLength(128);

        builder.Property(t => t.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(t => t.RevokedAt)
            .HasColumnName("revoked_at")
            .IsRequired();

        builder.HasIndex(t => t.Jti).IsUnique();
    }
}
