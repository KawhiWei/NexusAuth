using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Persistence.Configurations;

public sealed class SsoSessionConfiguration : IEntityTypeConfiguration<SsoSession>
{
    public void Configure(EntityTypeBuilder<SsoSession> builder)
    {
        builder.ToTable("sso_sessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).HasColumnName("id");
        builder.Property(session => session.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(session => session.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(session => session.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(session => session.RevokedAt).HasColumnName("revoked_at");
        builder.HasIndex(session => new { session.UserId, session.RevokedAt, session.ExpiresAt })
            .HasDatabaseName("ix_sso_sessions_user_active");
    }
}
