using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Persistence.Configurations;

public sealed class LoginAuditLogConfiguration : IEntityTypeConfiguration<LoginAuditLog>
{
    public void Configure(EntityTypeBuilder<LoginAuditLog> builder)
    {
        builder.ToTable("login_audit_logs");
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).HasColumnName("id");
        builder.Property(log => log.UserId).HasColumnName("user_id");
        builder.Property(log => log.Username).HasColumnName("username").HasMaxLength(100).IsRequired();
        builder.Property(log => log.ClientId).HasColumnName("client_id").HasMaxLength(128);
        builder.Property(log => log.IsSuccessful).HasColumnName("is_successful").IsRequired();
        builder.Property(log => log.FailureReason).HasColumnName("failure_reason").HasMaxLength(64);
        builder.Property(log => log.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
        builder.Property(log => log.UserAgent).HasColumnName("user_agent").HasMaxLength(1024);
        builder.Property(log => log.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.HasIndex(log => log.OccurredAt).HasDatabaseName("ix_login_audit_logs_occurred_at");
        builder.HasIndex(log => new { log.UserId, log.OccurredAt }).HasDatabaseName("ix_login_audit_logs_user_occurred_at");
        builder.HasIndex(log => new { log.ClientId, log.OccurredAt }).HasDatabaseName("ix_login_audit_logs_client_occurred_at");
    }
}
