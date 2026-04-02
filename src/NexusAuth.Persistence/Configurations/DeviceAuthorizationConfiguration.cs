using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Persistence.Configurations;

public class DeviceAuthorizationConfiguration : IEntityTypeConfiguration<DeviceAuthorization>
{
    public void Configure(EntityTypeBuilder<DeviceAuthorization> builder)
    {
        builder.ToTable("device_authorizations");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");

        builder.Property(d => d.DeviceCode)
            .HasColumnName("device_code")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(d => d.UserCode)
            .HasColumnName("user_code")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.UserCodeNormalized)
            .HasColumnName("user_code_normalized")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.ClientId)
            .HasColumnName("client_id")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(d => d.Scope)
            .HasColumnName("scope")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(d => d.UserId)
            .HasColumnName("user_id");

        builder.Property(d => d.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.PollingIntervalSeconds)
            .HasColumnName("polling_interval_seconds")
            .IsRequired();

        builder.Property(d => d.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(d => d.AuthorizedAt)
            .HasColumnName("authorized_at");

        builder.Property(d => d.LastPolledAt)
            .HasColumnName("last_polled_at");

        builder.HasIndex(d => d.DeviceCode).IsUnique();
        builder.HasIndex(d => d.UserCodeNormalized).IsUnique();
    }
}
